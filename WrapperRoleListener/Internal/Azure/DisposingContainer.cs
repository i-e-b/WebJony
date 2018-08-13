using System;
using System.Collections;
using System.Collections.Generic;

namespace WrapperRoleListener.Internal.Azure
{
    public class DisposingContainer<T> : IDisposable, ICollection<T>
        where T : IDisposable{

        private readonly List<T> _innerContainer;
        private readonly object _lock;
        private volatile bool _isDisposed;

        public DisposingContainer()
        {
            _isDisposed = false;
            _lock = new object();
            _innerContainer = new List<T>();
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                DisposeAll();
            }
        }

        private void DisposeAll()
        {
            foreach (var item in _innerContainer)
            {
                try { if (item != null) item.Dispose(); }
                catch { Ignore(); }
            }
        }

        private static void Ignore() { } 

        public IEnumerator<T> GetEnumerator() => _innerContainer.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public void Add(T item)
        {
            if (item == null) return;
            lock(_lock){_innerContainer.Add(item); }
        }

        /// <summary>
        /// Dispose all contained items then clear
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                if (_isDisposed) return;
                _isDisposed = true;
                DisposeAll();
                _innerContainer.Clear();

                _isDisposed = false;
            }
        }

        /// <inheritdoc />
        public bool Contains(T item) => _innerContainer.Contains(item);

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex) => _innerContainer.CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public bool Remove(T item)
        {
            if (item == null) return false;
            item.Dispose();
            return _innerContainer.Remove(item);
        }

        public int Count { get { return _innerContainer.Count; } }
        public bool IsReadOnly { get { return false; } }
    }
}