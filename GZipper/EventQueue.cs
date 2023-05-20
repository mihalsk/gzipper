using System;
using System.Collections.Concurrent;

namespace GZip
{
    class EventQueue<T> : ConcurrentQueue<T>
    {
        public event EventHandler Enqueued;

        protected virtual void OnEnqueued()
        {
            Enqueued?.Invoke(this, EventArgs.Empty);
        }

        public virtual new void Enqueue(T item)
        {
            base.Enqueue(item);
            OnEnqueued();
        }

        public event EventHandler Dequeued;

        protected virtual void OnDequeued()
        {
            Dequeued?.Invoke(this, EventArgs.Empty);
        }

        public virtual new bool TryDequeue(out T item)
        {
            bool success = base.TryDequeue(out item);
            if (success)
            {
                OnDequeued();
            }
            return success;
        }
    }
}
