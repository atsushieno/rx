﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

#if !NO_PERF
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace System.Reactive.Linq.Observαble
{
    class GroupByUntil<TSource, TKey, TElement, TDuration> : Producer<IGroupedObservable<TKey, TElement>>
    {
        private readonly IObservable<TSource> _source;
        private readonly Func<TSource, TKey> _keySelector;
        private readonly Func<TSource, TElement> _elementSelector;
        private readonly Func<IGroupedObservable<TKey, TElement>, IObservable<TDuration>> _durationSelector;
        private readonly IEqualityComparer<TKey> _comparer;

        public GroupByUntil(IObservable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, Func<IGroupedObservable<TKey, TElement>, IObservable<TDuration>> durationSelector, IEqualityComparer<TKey> comparer)
        {
            _source = source;
            _keySelector = keySelector;
            _elementSelector = elementSelector;
            _durationSelector = durationSelector;
            _comparer = comparer;
        }

        private CompositeDisposable _groupDisposable;
        private RefCountDisposable _refCountDisposable;

        protected override IDisposable Run(IObserver<IGroupedObservable<TKey, TElement>> observer, IDisposable cancel, Action<IDisposable> setSink)
        {
            _groupDisposable = new CompositeDisposable();
            _refCountDisposable = new RefCountDisposable(_groupDisposable);

            var sink = new _(this, observer, cancel);
            setSink(sink);
            _groupDisposable.Add(_source.SubscribeSafe(sink));

            return _refCountDisposable;
        }

        class _ : Sink<IGroupedObservable<TKey, TElement>>, IObserver<TSource>
        {
            private readonly GroupByUntil<TSource, TKey, TElement, TDuration> _parent;
            private readonly Map<TKey, ISubject<TElement>> _map;

            public _(GroupByUntil<TSource, TKey, TElement, TDuration> parent, IObserver<IGroupedObservable<TKey, TElement>> observer, IDisposable cancel)
                : base(observer, cancel)
            {
                _parent = parent;
                _map = new Map<TKey, ISubject<TElement>>(_parent._comparer);
            }

            public void OnNext(TSource value)
            {
                var key = default(TKey);
                try
                {
                    key = _parent._keySelector(value);
                }
                catch (Exception exception)
                {
                    Error(exception);
                    return;
                }

                var fireNewMapEntry = false;
                var writer = default(ISubject<TElement>);
                try
                {
                    writer = _map.GetOrAdd(key, () => new Subject<TElement>(), out fireNewMapEntry);
                }
                catch (Exception exception)
                {
                    Error(exception);
                    return;
                }

                if (fireNewMapEntry)
                {
                    var group = new GroupedObservable<TKey, TElement>(key, writer, _parent._refCountDisposable);

                    var duration = default(IObservable<TDuration>);

                    var durationGroup = new GroupedObservable<TKey, TElement>(key, writer);
                    try
                    {
                        duration = _parent._durationSelector(durationGroup);
                    }
                    catch (Exception exception)
                    {
                        Error(exception);
                        return;
                    }

                    lock (base._observer)
                        base._observer.OnNext(group);

                    var md = new SingleAssignmentDisposable();
                    _parent._groupDisposable.Add(md);
                    md.Disposable = duration.SubscribeSafe(new δ(this, key, writer, md));
                }

                var element = default(TElement);
                try
                {
                    element = _parent._elementSelector(value);
                }
                catch (Exception exception)
                {
                    Error(exception);
                    return;
                }

                writer.OnNext(element);
            }

            class δ : IObserver<TDuration>
            {
                private readonly _ _parent;
                private readonly TKey _key;
                private readonly ISubject<TElement> _writer;
                private readonly IDisposable _self;

                public δ(_ parent, TKey key, ISubject<TElement> writer, IDisposable self)
                {
                    _parent = parent;
                    _key = key;
                    _writer = writer;
                    _self = self;
                }

                public void OnNext(TDuration value)
                {
                    if (_parent._map.Remove(_key))
                        _writer.OnCompleted();

                    _parent._parent._groupDisposable.Remove(_self);
                }

                public void OnError(Exception error)
                {
                    _parent.Error(error);
                    _self.Dispose();
                }

                public void OnCompleted()
                {
                    if (_parent._map.Remove(_key))
                        _writer.OnCompleted();

                    _parent._parent._groupDisposable.Remove(_self);
                }
            }

            public void OnError(Exception error)
            {
                Error(error);
            }

            public void OnCompleted()
            {
                foreach (var w in _map.Values)
                    w.OnCompleted();

                lock (base._observer)
                    base._observer.OnCompleted();

                base.Dispose();
            }

            private void Error(Exception exception)
            {
                foreach (var w in _map.Values)
                    w.OnError(exception);

                lock (base._observer)
                    base._observer.OnError(exception);

                base.Dispose();
            }
        }
    }

#if !NO_CDS
    class Map<TKey, TValue>
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> _map;

        public Map(IEqualityComparer<TKey> comparer)
        {
            _map = new System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>(comparer);
        }

        public TValue GetOrAdd(TKey key, Func<TValue> valueFactory, out bool added)
        {
            added = false;

            var value = default(TValue);
            var newValue = default(TValue);
            var hasNewValue = false;
            while (true)
            {
                if (_map.TryGetValue(key, out value))
                    break;

                if (!hasNewValue)
                {
                    newValue = valueFactory();
                    hasNewValue = true;
                }

                if (_map.TryAdd(key, newValue))
                {
                    added = true;
                    value = newValue;
                    break;
                }
            }

            return value;
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                return _map.Values.ToArray();
            }
        }

        public bool Remove(TKey key)
        {
            var value = default(TValue);
            return _map.TryRemove(key, out value);
        }
    }
#else
    class Map<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _map;

        public Map(IEqualityComparer<TKey> comparer)
        {
            _map = new Dictionary<TKey, TValue>(comparer);
        }

        public TValue GetOrAdd(TKey key, Func<TValue> valueFactory, out bool added)
        {
            lock (_map)
            {
                added = false;

                var value = default(TValue);
                if (!_map.TryGetValue(key, out value))
                {
                    value = valueFactory();
                    _map.Add(key, value);
                    added = true;
                }

                return value;
            }
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                lock (_map)
                {
                    return _map.Values.ToArray();
                }
            }
        }

        public bool Remove(TKey key)
        {
            lock (_map)
            {
                return _map.Remove(key);
            }
        }
    }
#endif
}
#endif