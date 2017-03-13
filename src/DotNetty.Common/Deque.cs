// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nito
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    ///     A double-ended queue (deque), which provides O(1) indexed access, O(1) removals from the front and back, amortized
    ///     O(1) insertions to the front and back, and O(N) insertions and removals anywhere else (with the operations getting
    ///     slower as the index approaches the middle).
    /// </summary>
    /// <typeparam name="T">The type of elements contained in the deque.</typeparam>
    [DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
    [DebuggerTypeProxy(typeof(Deque<>.DebugView))]
    sealed class Deque<T> : IList<T>, IList
    {
        /// <summary>
        ///     The default capacity.
        /// </summary>
        const int DefaultCapacity = 8;

        /// <summary>
        ///     The circular buffer that holds the view.
        /// </summary>
        T[] buffer;

        /// <summary>
        ///     The offset into <see cref="buffer" /> where the view begins.
        /// </summary>
        int offset;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Deque&lt;T&gt;" /> class with the specified capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity. Must be greater than <c>0</c>.</param>
        public Deque(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0.");
            this.buffer = new T[capacity];
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Deque&lt;T&gt;" /> class with the elements from the specified
        ///     collection.
        /// </summary>
        /// <param name="collection">The collection.</param>
        public Deque(IEnumerable<T> collection)
        {
            int count = collection.Count();
            if (count > 0)
            {
                this.buffer = new T[count];
                this.DoInsertRange(0, collection, count);
            }
            else
            {
                this.buffer = new T[DefaultCapacity];
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Deque&lt;T&gt;" /> class.
        /// </summary>
        public Deque()
            : this(DefaultCapacity)
        {
        }

        #region GenericListImplementations

        /// <summary>
        ///     Gets a value indicating whether this list is read-only. This implementation always returns <c>false</c>.
        /// </summary>
        /// <returns>true if this list is read-only; otherwise, false.</returns>
        bool ICollection<T>.IsReadOnly => false;

        /// <summary>
        ///     Gets or sets the item at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to get or set.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in this list.</exception>
        /// <exception cref="T:System.NotSupportedException">This property is set and the list is read-only.</exception>
        public T this[int index]
        {
            get
            {
                CheckExistingIndexArgument(this.Count, index);
                return this.DoGetItem(index);
            }

            set
            {
                CheckExistingIndexArgument(this.Count, index);
                this.DoSetItem(index, value);
            }
        }

        /// <summary>
        ///     Inserts an item to this list at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
        /// <param name="item">The object to insert into this list.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     <paramref name="index" /> is not a valid index in this list.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        ///     This list is read-only.
        /// </exception>
        public void Insert(int index, T item)
        {
            CheckNewIndexArgument(this.Count, index);
            this.DoInsert(index, item);
        }

        /// <summary>
        ///     Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     <paramref name="index" /> is not a valid index in this list.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        ///     This list is read-only.
        /// </exception>
        public void RemoveAt(int index)
        {
            CheckExistingIndexArgument(this.Count, index);
            this.DoRemoveAt(index);
        }

        /// <summary>
        ///     Determines the index of a specific item in this list.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns>The index of <paramref name="item" /> if found in this list; otherwise, -1.</returns>
        public int IndexOf(T item)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            int ret = 0;
            foreach (T sourceItem in this)
            {
                if (comparer.Equals(item, sourceItem))
                    return ret;
                ++ret;
            }

            return -1;
        }

        /// <summary>
        ///     Adds an item to the end of this list.
        /// </summary>
        /// <param name="item">The object to add to this list.</param>
        /// <exception cref="T:System.NotSupportedException">
        ///     This list is read-only.
        /// </exception>
        void ICollection<T>.Add(T item)
        {
            this.DoInsert(this.Count, item);
        }

        /// <summary>
        ///     Determines whether this list contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns>
        ///     true if <paramref name="item" /> is found in this list; otherwise, false.
        /// </returns>
        bool ICollection<T>.Contains(T item)
        {
            return this.Contains(item, null);
        }

        /// <summary>
        ///     Copies the elements of this list to an <see cref="T:System.Array" />, starting at a particular
        ///     <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">
        ///     The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied
        ///     from this slice. The <see cref="T:System.Array" /> must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException">
        ///     <paramref name="array" /> is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        ///     <paramref name="arrayIndex" /> is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        ///     <paramref name="arrayIndex" /> is equal to or greater than the length of <paramref name="array" />.
        ///     -or-
        ///     The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1" /> is greater than the
        ///     available space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.
        /// </exception>
        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array), "Array is null");

            int count = this.Count;
            CheckRangeArguments(array.Length, arrayIndex, count);
            for (int i = 0; i != count; ++i)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        /// <summary>
        ///     Removes the first occurrence of a specific object from this list.
        /// </summary>
        /// <param name="item">The object to remove from this list.</param>
        /// <returns>
        ///     true if <paramref name="item" /> was successfully removed from this list; otherwise, false. This method also
        ///     returns false if <paramref name="item" /> is not found in this list.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">
        ///     This list is read-only.
        /// </exception>
        public bool Remove(T item)
        {
            int index = this.IndexOf(item);
            if (index == -1)
                return false;

            this.DoRemoveAt(index);
            return true;
        }

        /// <summary>
        ///     Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        ///     A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            int count = this.Count;
            for (int i = 0; i != count; ++i)
            {
                yield return this.DoGetItem(i);
            }
        }

        /// <summary>
        ///     Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        ///     An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion

        #region ObjectListImplementations

        /// <summary>
        ///     Returns whether or not the type of a given item indicates it is appropriate for storing in this container.
        /// </summary>
        /// <param name="item">The item to test.</param>
        /// <returns><c>true</c> if the item is appropriate to store in this container; otherwise, <c>false</c>.</returns>
        bool ObjectIsT(object item)
        {
            if (item is T)
            {
                return true;
            }

            if (item == null)
            {
                TypeInfo typeInfo = typeof(T).GetTypeInfo();
                if (typeInfo.IsClass && !typeInfo.IsPointer)
                    return true; // classes, arrays, and delegates
                if (typeInfo.IsInterface)
                    return true; // interfaces
                if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return true; // nullable value types
            }

            return false;
        }

        int IList.Add(object value)
        {
            if (!this.ObjectIsT(value))
                throw new ArgumentException("Item is not of the correct type.", nameof(value));
            this.AddToBack((T)value);
            return this.Count - 1;
        }

        bool IList.Contains(object value)
        {
            if (!this.ObjectIsT(value))
                throw new ArgumentException("Item is not of the correct type.", nameof(value));
            return this.Contains((T)value);
        }

        int IList.IndexOf(object value)
        {
            if (!this.ObjectIsT(value))
                throw new ArgumentException("Item is not of the correct type.", nameof(value));
            return this.IndexOf((T)value);
        }

        void IList.Insert(int index, object value)
        {
            if (!this.ObjectIsT(value))
                throw new ArgumentException("Item is not of the correct type.", nameof(value));
            this.Insert(index, (T)value);
        }

        bool IList.IsFixedSize => false;

        bool IList.IsReadOnly => false;

        void IList.Remove(object value)
        {
            if (!this.ObjectIsT(value))
                throw new ArgumentException("Item is not of the correct type.", nameof(value));
            this.Remove((T)value);
        }

        object IList.this[int index]
        {
            get { return this[index]; }

            set
            {
                if (!this.ObjectIsT(value))
                    throw new ArgumentException("Item is not of the correct type.", nameof(value));
                this[index] = (T)value;
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array), "Destination array cannot be null.");
            CheckRangeArguments(array.Length, index, this.Count);

            for (int i = 0; i != this.Count; ++i)
            {
                try
                {
                    array.SetValue(this[i], index + i);
                }
                catch (InvalidCastException ex)
                {
                    throw new ArgumentException("Destination array is of incorrect type.", ex);
                }
            }
        }

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        #endregion

        #region GenericListHelpers

        /// <summary>
        ///     Checks the <paramref name="index" /> argument to see if it refers to a valid insertion point in a source of a given
        ///     length.
        /// </summary>
        /// <param name="sourceLength">The length of the source. This parameter is not checked for validity.</param>
        /// <param name="index">The index into the source.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="index" /> is not a valid index to an insertion point for
        ///     the source.
        /// </exception>
        static void CheckNewIndexArgument(int sourceLength, int index)
        {
            if (index < 0 || index > sourceLength)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Invalid new index " + index + " for source length " + sourceLength);
            }
        }

        /// <summary>
        ///     Checks the <paramref name="index" /> argument to see if it refers to an existing element in a source of a given
        ///     length.
        /// </summary>
        /// <param name="sourceLength">The length of the source. This parameter is not checked for validity.</param>
        /// <param name="index">The index into the source.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="index" /> is not a valid index to an existing element for
        ///     the source.
        /// </exception>
        static void CheckExistingIndexArgument(int sourceLength, int index)
        {
            if (index < 0 || index >= sourceLength)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Invalid existing index " + index + " for source length " + sourceLength);
            }
        }

        /// <summary>
        ///     Checks the <paramref name="offset" /> and <paramref name="count" /> arguments for validity when applied to a source
        ///     of a given length. Allows 0-element ranges, including a 0-element range at the end of the source.
        /// </summary>
        /// <param name="sourceLength">The length of the source. This parameter is not checked for validity.</param>
        /// <param name="offset">The index into source at which the range begins.</param>
        /// <param name="count">The number of elements in the range.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Either <paramref name="offset" /> or <paramref name="count" /> is less
        ///     than 0.
        /// </exception>
        /// <exception cref="ArgumentException">The range [offset, offset + count) is not within the range [0, sourceLength).</exception>
        static void CheckRangeArguments(int sourceLength, int offset, int count)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset " + offset);
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Invalid count " + count);
            }

            if (sourceLength - offset < count)
            {
                throw new ArgumentException("Invalid offset (" + offset + ") or count + (" + count + ") for source length " + sourceLength);
            }
        }

        #endregion

        /// <summary>
        ///     Gets a value indicating whether this instance is empty.
        /// </summary>
        bool IsEmpty => this.Count == 0;

        /// <summary>
        ///     Gets a value indicating whether this instance is at full capacity.
        /// </summary>
        bool IsFull => this.Count == this.Capacity;

        /// <summary>
        ///     Gets a value indicating whether the buffer is "split" (meaning the beginning of the view is at a later index in
        ///     <see cref="buffer" /> than the end).
        /// </summary>
        bool IsSplit => this.offset > (this.Capacity - this.Count);

        /// <summary>
        ///     Gets or sets the capacity for this deque. This value must always be greater than zero, and this property cannot be
        ///     set to a value less than <see cref="Count" />.
        /// </summary>
        /// <exception cref="InvalidOperationException"><c>Capacity</c> cannot be set to a value less than <see cref="Count" />.</exception>
        public int Capacity
        {
            get { return this.buffer.Length; }

            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Capacity must be greater than 0.");

                if (value < this.Count)
                    throw new InvalidOperationException("Capacity cannot be set to a value less than Count");

                if (value == this.buffer.Length)
                    return;

                // Create the new buffer and copy our existing range.
                var newBuffer = new T[value];
                if (this.IsSplit)
                {
                    // The existing buffer is split, so we have to copy it in parts
                    int length = this.Capacity - this.offset;
                    Array.Copy(this.buffer, this.offset, newBuffer, 0, length);
                    Array.Copy(this.buffer, 0, newBuffer, length, this.Count - length);
                }
                else
                {
                    // The existing buffer is whole
                    Array.Copy(this.buffer, this.offset, newBuffer, 0, this.Count);
                }

                // Set up to use the new buffer.
                this.buffer = newBuffer;
                this.offset = 0;
            }
        }

        /// <summary>
        ///     Gets the number of elements contained in this deque.
        /// </summary>
        /// <returns>The number of elements contained in this deque.</returns>
        public int Count { get; private set; }

        /// <summary>
        ///     Applies the offset to <paramref name="index" />, resulting in a buffer index.
        /// </summary>
        /// <param name="index">The deque index.</param>
        /// <returns>The buffer index.</returns>
        int DequeIndexToBufferIndex(int index)
        {
            return (index + this.offset) % this.Capacity;
        }

        /// <summary>
        ///     Gets an element at the specified view index.
        /// </summary>
        /// <param name="index">The zero-based view index of the element to get. This index is guaranteed to be valid.</param>
        /// <returns>The element at the specified index.</returns>
        T DoGetItem(int index)
        {
            return this.buffer[this.DequeIndexToBufferIndex(index)];
        }

        /// <summary>
        ///     Sets an element at the specified view index.
        /// </summary>
        /// <param name="index">The zero-based view index of the element to get. This index is guaranteed to be valid.</param>
        /// <param name="item">The element to store in the list.</param>
        void DoSetItem(int index, T item)
        {
            this.buffer[this.DequeIndexToBufferIndex(index)] = item;
        }

        /// <summary>
        ///     Inserts an element at the specified view index.
        /// </summary>
        /// <param name="index">
        ///     The zero-based view index at which the element should be inserted. This index is guaranteed to be
        ///     valid.
        /// </param>
        /// <param name="item">The element to store in the list.</param>
        void DoInsert(int index, T item)
        {
            this.EnsureCapacityForOneElement();

            if (index == 0)
            {
                this.DoAddToFront(item);
                return;
            }
            else if (index == this.Count)
            {
                this.DoAddToBack(item);
                return;
            }

            this.DoInsertRange(index, new[] { item }, 1);
        }

        /// <summary>
        ///     Removes an element at the specified view index.
        /// </summary>
        /// <param name="index">The zero-based view index of the element to remove. This index is guaranteed to be valid.</param>
        void DoRemoveAt(int index)
        {
            if (index == 0)
            {
                this.DoRemoveFromFront();
                return;
            }
            else if (index == this.Count - 1)
            {
                this.DoRemoveFromBack();
                return;
            }

            this.DoRemoveRange(index, 1);
        }

        /// <summary>
        ///     Increments <see cref="offset" /> by <paramref name="value" /> using modulo-<see cref="Capacity" /> arithmetic.
        /// </summary>
        /// <param name="value">The value by which to increase <see cref="offset" />. May not be negative.</param>
        /// <returns>The value of <see cref="offset" /> after it was incremented.</returns>
        int PostIncrement(int value)
        {
            int ret = this.offset;
            this.offset += value;
            this.offset %= this.Capacity;
            return ret;
        }

        /// <summary>
        ///     Decrements <see cref="offset" /> by <paramref name="value" /> using modulo-<see cref="Capacity" /> arithmetic.
        /// </summary>
        /// <param name="value">
        ///     The value by which to reduce <see cref="offset" />. May not be negative or greater than
        ///     <see cref="Capacity" />.
        /// </param>
        /// <returns>The value of <see cref="offset" /> before it was decremented.</returns>
        int PreDecrement(int value)
        {
            this.offset -= value;
            if (this.offset < 0)
                this.offset += this.Capacity;
            return this.offset;
        }

        /// <summary>
        ///     Inserts a single element to the back of the view. <see cref="IsFull" /> must be false when this method is called.
        /// </summary>
        /// <param name="value">The element to insert.</param>
        void DoAddToBack(T value)
        {
            this.buffer[this.DequeIndexToBufferIndex(this.Count)] = value;
            ++this.Count;
        }

        /// <summary>
        ///     Inserts a single element to the front of the view. <see cref="IsFull" /> must be false when this method is called.
        /// </summary>
        /// <param name="value">The element to insert.</param>
        void DoAddToFront(T value)
        {
            this.buffer[this.PreDecrement(1)] = value;
            ++this.Count;
        }

        /// <summary>
        ///     Removes and returns the last element in the view. <see cref="IsEmpty" /> must be false when this method is called.
        /// </summary>
        /// <returns>The former last element.</returns>
        T DoRemoveFromBack()
        {
            T ret = this.buffer[this.DequeIndexToBufferIndex(this.Count - 1)];
            --this.Count;
            return ret;
        }

        /// <summary>
        ///     Removes and returns the first element in the view. <see cref="IsEmpty" /> must be false when this method is called.
        /// </summary>
        /// <returns>The former first element.</returns>
        T DoRemoveFromFront()
        {
            --this.Count;
            return this.buffer[this.PostIncrement(1)];
        }

        /// <summary>
        ///     Inserts a range of elements into the view.
        /// </summary>
        /// <param name="index">The index into the view at which the elements are to be inserted.</param>
        /// <param name="collection">The elements to insert.</param>
        /// <param name="collectionCount">
        ///     The number of elements in <paramref name="collection" />. Must be greater than zero, and
        ///     the sum of <paramref name="collectionCount" /> and <see cref="Count" /> must be less than or equal to
        ///     <see cref="Capacity" />.
        /// </param>
        void DoInsertRange(int index, IEnumerable<T> collection, int collectionCount)
        {
            // Make room in the existing list
            if (index < this.Count / 2)
            {
                // Inserting into the first half of the list

                // Move lower items down: [0, index) -> [Capacity - collectionCount, Capacity - collectionCount + index)
                // This clears out the low "index" number of items, moving them "collectionCount" places down;
                //   after rotation, there will be a "collectionCount"-sized hole at "index".
                int copyCount = index;
                int writeIndex = this.Capacity - collectionCount;
                for (int j = 0; j != copyCount; ++j)
                    this.buffer[this.DequeIndexToBufferIndex(writeIndex + j)] = this.buffer[this.DequeIndexToBufferIndex(j)];

                // Rotate to the new view
                this.PreDecrement(collectionCount);
            }
            else
            {
                // Inserting into the second half of the list

                // Move higher items up: [index, count) -> [index + collectionCount, collectionCount + count)
                int copyCount = this.Count - index;
                int writeIndex = index + collectionCount;
                for (int j = copyCount - 1; j != -1; --j)
                    this.buffer[this.DequeIndexToBufferIndex(writeIndex + j)] = this.buffer[this.DequeIndexToBufferIndex(index + j)];
            }

            // Copy new items into place
            int i = index;
            foreach (T item in collection)
            {
                this.buffer[this.DequeIndexToBufferIndex(i)] = item;
                ++i;
            }

            // Adjust valid count
            this.Count += collectionCount;
        }

        /// <summary>
        ///     Removes a range of elements from the view.
        /// </summary>
        /// <param name="index">The index into the view at which the range begins.</param>
        /// <param name="collectionCount">
        ///     The number of elements in the range. This must be greater than 0 and less than or equal
        ///     to <see cref="Count" />.
        /// </param>
        void DoRemoveRange(int index, int collectionCount)
        {
            if (index == 0)
            {
                // Removing from the beginning: rotate to the new view
                this.PostIncrement(collectionCount);
                this.Count -= collectionCount;
                return;
            }
            else if (index == this.Count - collectionCount)
            {
                // Removing from the ending: trim the existing view
                this.Count -= collectionCount;
                return;
            }

            if ((index + (collectionCount / 2)) < this.Count / 2)
            {
                // Removing from first half of list

                // Move lower items up: [0, index) -> [collectionCount, collectionCount + index)
                int copyCount = index;
                int writeIndex = collectionCount;
                for (int j = copyCount - 1; j != -1; --j)
                    this.buffer[this.DequeIndexToBufferIndex(writeIndex + j)] = this.buffer[this.DequeIndexToBufferIndex(j)];

                // Rotate to new view
                this.PostIncrement(collectionCount);
            }
            else
            {
                // Removing from second half of list

                // Move higher items down: [index + collectionCount, count) -> [index, count - collectionCount)
                int copyCount = this.Count - collectionCount - index;
                int readIndex = index + collectionCount;
                for (int j = 0; j != copyCount; ++j)
                    this.buffer[this.DequeIndexToBufferIndex(index + j)] = this.buffer[this.DequeIndexToBufferIndex(readIndex + j)];
            }

            // Adjust valid count
            this.Count -= collectionCount;
        }

        /// <summary>
        ///     Doubles the capacity if necessary to make room for one more element. When this method returns,
        ///     <see cref="IsFull" /> is false.
        /// </summary>
        void EnsureCapacityForOneElement()
        {
            if (this.IsFull)
            {
                this.Capacity = this.Capacity * 2;
            }
        }

        /// <summary>
        ///     Inserts a single element at the back of this deque.
        /// </summary>
        /// <param name="value">The element to insert.</param>
        public void AddToBack(T value)
        {
            this.EnsureCapacityForOneElement();
            this.DoAddToBack(value);
        }

        /// <summary>
        ///     Inserts a single element at the front of this deque.
        /// </summary>
        /// <param name="value">The element to insert.</param>
        public void AddToFront(T value)
        {
            this.EnsureCapacityForOneElement();
            this.DoAddToFront(value);
        }

        /// <summary>
        ///     Inserts a collection of elements into this deque.
        /// </summary>
        /// <param name="index">The index at which the collection is inserted.</param>
        /// <param name="collection">The collection of elements to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="index" /> is not a valid index to an insertion point for
        ///     the source.
        /// </exception>
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            int collectionCount = collection.Count();
            CheckNewIndexArgument(this.Count, index);

            // Overflow-safe check for "this.Count + collectionCount > this.Capacity"
            if (collectionCount > this.Capacity - this.Count)
            {
                this.Capacity = checked(this.Count + collectionCount);
            }

            if (collectionCount == 0)
            {
                return;
            }

            this.DoInsertRange(index, collection, collectionCount);
        }

        /// <summary>
        ///     Removes a range of elements from this deque.
        /// </summary>
        /// <param name="offset">The index into the deque at which the range begins.</param>
        /// <param name="count">The number of elements to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Either <paramref name="offset" /> or <paramref name="count" /> is less
        ///     than 0.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     The range [<paramref name="offset" />, <paramref name="offset" /> +
        ///     <paramref name="count" />) is not within the range [0, <see cref="Count" />).
        /// </exception>
        public void RemoveRange(int offset, int count)
        {
            CheckRangeArguments(this.Count, offset, count);

            if (count == 0)
            {
                return;
            }

            this.DoRemoveRange(offset, count);
        }

        /// <summary>
        ///     Removes and returns the last element of this deque.
        /// </summary>
        /// <returns>The former last element.</returns>
        /// <exception cref="InvalidOperationException">The deque is empty.</exception>
        public T RemoveFromBack()
        {
            if (this.IsEmpty)
                throw new InvalidOperationException("The deque is empty.");

            return this.DoRemoveFromBack();
        }

        /// <summary>
        ///     Removes and returns the first element of this deque.
        /// </summary>
        /// <returns>The former first element.</returns>
        /// <exception cref="InvalidOperationException">The deque is empty.</exception>
        public T RemoveFromFront()
        {
            if (this.IsEmpty)
                throw new InvalidOperationException("The deque is empty.");

            return this.DoRemoveFromFront();
        }

        /// <summary>
        ///     Removes all items from this deque.
        /// </summary>
        public void Clear()
        {
            this.offset = 0;
            this.Count = 0;
        }

        [DebuggerNonUserCode]
        sealed class DebugView
        {
            readonly Deque<T> deque;

            public DebugView(Deque<T> deque)
            {
                this.deque = deque;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public T[] Items
            {
                get
                {
                    var array = new T[this.deque.Count];
                    ((ICollection<T>)this.deque).CopyTo(array, 0);
                    return array;
                }
            }
        }
    }
}
