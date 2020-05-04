using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.Collections
{

	/// <summary>
	/// Represents a queue with indexed access to the items
	/// </summary>
	/// <typeparam name="T">The type of items in the queue</typeparam>
	// source : https://www.codeproject.com/Tips/5252993/IndexedQueue-T-A-Custom-Queue-in-Csharp
	public sealed class IndexedQueue<T> : IEnumerable<T>, ICollection<T>
	{
		const int _DefaultCapacity = 16;
		const float _GrowthFactor = 2f;
		const float _TrimThreshold = .9f;
		T[] _array;
		int _head;
		int _count;
		/// <summary>
		/// Creates a queue with the specified capacity
		/// </summary>
		/// <param name="capacity">The initial capacity of the queue</param>
		public IndexedQueue(int capacity)
		{
			if (0 >= capacity)
				throw new ArgumentOutOfRangeException("The capacity must be greater than zero.", nameof(capacity));
			_array = new T[capacity];
			_head = 0;
			_count = 0;
			_version = 0;
		}
		/// <summary>
		/// Gets or sets the value at the index
		/// </summary>
		/// <param name="index">The index</param>
		/// <returns>The value at the specified index</returns>
		public T this[int index]
		{
			get
			{
				if (0 > index || index >= _count)
					throw new IndexOutOfRangeException();
				return _array[(index + _head) % _array.Length];
			}
			set
			{
				if (0 > index || index >= _count)
					throw new IndexOutOfRangeException();
				_array[(index + _head) % _array.Length] = value;
				++_version;
			}
		}
		/// <summary>
		/// Creates a queue with the default capacity
		/// </summary>
		public IndexedQueue() : this(_DefaultCapacity)
		{

		}
		/// <summary>
		/// Creates a queue filled with the specified items
		/// </summary>
		/// <param name="collection">The collection of items to fill the queue with</param>
		public IndexedQueue(IEnumerable<T> collection)
		{
			if (null == collection)
				throw new ArgumentNullException(nameof(collection));
			foreach (var item in collection)
				Enqueue(item);
		}
		/// <summary>
		/// Provides access to the item at the front of the queue without dequeueing it
		/// </summary>
		/// <returns>The frontmost item</returns>
		public T Peek()
		{
			if (0 == _count)
				throw new InvalidOperationException("The queue is empty.");
			return _array[_head];
		}
		/// <summary>
		/// Returns an array of the items in the queue
		/// </summary>
		/// <returns>An array containing the queue's items</returns>
		public T[] ToArray()
		{
			var result = new T[_count];
			CopyTo(result, 0);
			return result;
		}
		/// <summary>
		/// Inserts an item at the rear of the queue
		/// </summary>
		/// <param name="item">The item to insert</param>
		public void Enqueue(T item)
		{
			if (_count == _array.Length)
			{
				var arr = new T[(int)(_array.Length * _GrowthFactor)];
				if (_head + _count <= _array.Length)
				{
					Array.Copy(_array, arr, _count);
					_head = 0;
					arr[_count] = item;
					++_count;
					unchecked
					{
						++_version;
					}
					_array = arr;
				}
				else // if(_head+_count<=arr.Length)
				{
					Array.Copy(_array, _head, arr, 0, _array.Length - _head);
					Array.Copy(_array, 0, arr, _array.Length - _head, _head);
					_head = 0;
					arr[_count] = item;
					++_count;
					unchecked
					{
						++_version;
					}
					_array = arr;
				}
			}
			else
			{
				_array[(_head + _count) % _array.Length] = item;
				++_count;
				unchecked
				{
					++_version;
				}
			}
		}
		/// <summary>
		/// Removes an item from the front of the queue, returning it
		/// </summary>
		/// <returns>The item that was removed</returns>
		public T Dequeue()
		{
			if (0 == _count)
				throw new InvalidOperationException("The queue is empty");
			var result = _array[_head];
			++_head;
			_head = _head % _array.Length;
			--_count;
			unchecked
			{
				++_version;
			}
			return result;
		}
		/// <summary>
		/// Trims the extra array space that isn't being used.
		/// </summary>
		public void TrimExcess()
		{
			if (0 == _count)
			{
				_array = new T[_DefaultCapacity];
			}
			if (_array.Length * _TrimThreshold >= _count)
			{
				var arr = new T[_count];
				CopyTo(arr, 0);
				_head = 0;
				_array = arr;
				unchecked
				{
					++_version;
				}
			}
		}
		/// <summary>
		/// Attempts to return an item from the front of the queue without removing it
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True if the queue returned an item or false if the queue is empty</returns>
		public bool TryPeek(out T item)
		{
			if (0 != _count)
			{
				item = _array[_head];
				return true;
			}
			item = default(T);
			return false;
		}
		/// <summary>
		/// Attempts to return an item from the front of the queue, removing it
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True if the queue returned an item or false if the queue is empty</returns>
		public bool TryDequeue(out T item)
		{
			if (0 < _count)
			{
				item = _array[_head];
				++_head;
				_head = _head % _array.Length;
				--_count;
				unchecked
				{
					++_version;
				}
				return true;
			}
			item = default(T);
			return false;
		}

		#region IEnumerable<T> implementation

		int _version;
		public IEnumerator<T> GetEnumerator()
			=> new Enumerator(this);
		// legacy enumerable support (required)
		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
		struct Enumerator : IEnumerator<T>
		{
			const int _AfterEnd = -1;
			const int _BeforeStart = -2;
			const int _Disposed = -3;

			IndexedQueue<T> _outer;
			int _index;
			int _version;
			public Enumerator(IndexedQueue<T> outer)
			{
				_outer = outer;
				_version = outer._version;
				_index = _BeforeStart;
			}
			public T Current
			{
				get
				{
					switch (_index)
					{
						case _Disposed:
							throw new ObjectDisposedException(GetType().Name);
						case _BeforeStart:
							throw new InvalidOperationException("The cursor is before the start of the enumeration.");
						case _AfterEnd:
							throw new InvalidOperationException("The cursor is after the end of the enumeration.");
					}
					_CheckVersion();
					return _outer._array[_index % _outer._array.Length];
				}
			}
			// legacy enumerator support (required)
			object IEnumerator.Current
				=> Current;
			public bool MoveNext()
			{
				switch (_index)
				{
					case _Disposed:
						throw new ObjectDisposedException(GetType().Name);
					case _AfterEnd:
						return false;
					case _BeforeStart:
						_CheckVersion();
						if (0 == _outer._count)
						{
							_index = _AfterEnd;
							return false;
						}
						_index = _outer._head;
						return true;
				}
				_CheckVersion();
				if (++_index >= _outer._count + _outer._head)
				{
					_index = _AfterEnd;
					return false;
				}
				return true;
			}
			public void Reset()
			{
				if (-3 == _index)
					throw new ObjectDisposedException(GetType().Name);
				_CheckVersion();
				_index = _BeforeStart;
			}
			public void Dispose()
			{
				_index = _Disposed;
			}
			void _CheckVersion()
			{
				if (_version != _outer._version)
					throw new InvalidOperationException("The enumeration has changed and may not execute.");
			}
		}

		#endregion

		#region ICollection<T> implementation

		/// <summary>
		/// Indicates the count of items in the queue
		/// </summary>
		public int Count => _count;
		bool ICollection<T>.IsReadOnly => false;

		void ICollection<T>.Add(T item)
			=> Enqueue(item);

		/// <summary>
		/// Clears the items from the queue
		/// </summary>
		public void Clear()
		{
			_head = 0;
			_count = 0;
			unchecked
			{
				++_version;
			}
		}
		/// <summary>
		/// Indicates whether the queue contains an item
		/// </summary>
		/// <param name="item">The item to search for</param>
		/// <returns>True if the item was found, otherwise false</returns>
		public bool Contains(T item)
		{
			// TODO: Reimplement this using IndexOf()
			for (var i = 0; i < _count; ++i)
				if (Equals(_array[(_head + i) % _array.Length], item))
					return true;
			return false;
		}
		/// <summary>
		/// Copys the queue's items to a destination array
		/// </summary>
		/// <param name="array">The destination array</param>
		/// <param name="arrayIndex">The index in the destination to start copying at</param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			if (null == array) throw new ArgumentNullException(nameof(array));
			if (0 > arrayIndex || array.Length < arrayIndex + _count)
				throw new ArgumentOutOfRangeException(nameof(arrayIndex));
			// TODO: Reimplement this using Array.Copy
			for (var i = 0; i < _count; ++i)
				array[arrayIndex + i] = _array[(_head + i) % _array.Length];
		}

		bool ICollection<T>.Remove(T item)
		{
			if (Equals(_array[_head], item))
			{
				Dequeue();
				return true;
			}
			else
			{
				// TODO: Reimplement using RemoveAt()/IndexOf()
				for (var i = 0; i < _count; ++i)
				{
					var idx = (_head + i) % _array.Length;
					if (Equals(_array[idx], item))
					{
						if (_head + _count < _array.Length)
						{
							Array.Copy(_array, idx + 1, _array, idx, _count - idx - 1);
						}
						else if (idx == _array.Length - 1)
						{
							_array[idx] = _array[0];
							if (_count + _head != _array.Length)
							{
								Array.Copy(_array, 1, _array, 0, (_count + _head) % _array.Length - 1);
							}
						}
						else if (idx < _head)
						{
							Array.Copy(_array, idx + 1, _array, idx, (_count + _head) % _array.Length - 1);
						}
						--_count;
						unchecked
						{
							++_version;
						}
						return true;
					}
				}
			}
			return false;
		}

		#endregion
	}


	/// <summary>
	/// A double-ended queue (deque), which provides O(1) indexed access, O(1) removals from the front and back, amortized O(1) insertions to the front and back, and O(N) insertions and removals anywhere else (with the operations getting slower as the index approaches the middle).
	/// </summary>
	/// <typeparam name="T">The type of elements contained in the deque.</typeparam>
	// source : https://github.com/StephenCleary/Deque
	public sealed class Deque<T> : IList<T>, IReadOnlyList<T>, IList
	{
		/// <summary>
		/// The default capacity.
		/// </summary>
		private const int DefaultCapacity = 8;

		/// <summary>
		/// The circular _buffer that holds the view.
		/// </summary>
		private T[] _buffer;

		/// <summary>
		/// The offset into <see cref="_buffer"/> where the view begins.
		/// </summary>
		private int _offset;

		/// <summary>
		/// Initializes a new instance of the <see cref="Deque&lt;T&gt;"/> class with the specified capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity. Must be greater than <c>0</c>.</param>
		public Deque(int capacity)
		{
			if (capacity < 0)
				throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity may not be negative.");
			_buffer = new T[capacity];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Deque&lt;T&gt;"/> class with the elements from the specified collection.
		/// </summary>
		/// <param name="collection">The collection. May not be <c>null</c>.</param>
		public Deque(IEnumerable<T> collection)
		{
			if (collection == null)
				throw new ArgumentNullException(nameof(collection));

			var source = CollectionHelpers.ReifyCollection(collection);
			var count = source.Count;
			if (count > 0)
			{
				_buffer = new T[count];
				DoInsertRange(0, source);
			}
			else
			{
				_buffer = new T[DefaultCapacity];
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Deque&lt;T&gt;"/> class.
		/// </summary>
		public Deque()
			: this(DefaultCapacity)
		{
		}

		#region GenericListImplementations

		/// <summary>
		/// Gets a value indicating whether this list is read-only. This implementation always returns <c>false</c>.
		/// </summary>
		/// <returns>true if this list is read-only; otherwise, false.</returns>
		bool ICollection<T>.IsReadOnly
		{
			get { return false; }
		}

		/// <summary>
		/// Gets or sets the item at the specified index.
		/// </summary>
		/// <param name="index">The index of the item to get or set.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in this list.</exception>
		/// <exception cref="T:System.NotSupportedException">This property is set and the list is read-only.</exception>
		public T this[int index]
		{
			get
			{
				CheckExistingIndexArgument(Count, index);
				return DoGetItem(index);
			}

			set
			{
				CheckExistingIndexArgument(Count, index);
				DoSetItem(index, value);
			}
		}

		/// <summary>
		/// Inserts an item to this list at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
		/// <param name="item">The object to insert into this list.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid index in this list.
		/// </exception>
		/// <exception cref="T:System.NotSupportedException">
		/// This list is read-only.
		/// </exception>
		public void Insert(int index, T item)
		{
			CheckNewIndexArgument(Count, index);
			DoInsert(index, item);
		}

		/// <summary>
		/// Removes the item at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the item to remove.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid index in this list.
		/// </exception>
		/// <exception cref="T:System.NotSupportedException">
		/// This list is read-only.
		/// </exception>
		public void RemoveAt(int index)
		{
			CheckExistingIndexArgument(Count, index);
			DoRemoveAt(index);
		}

		/// <summary>
		/// Determines the index of a specific item in this list.
		/// </summary>
		/// <param name="item">The object to locate in this list.</param>
		/// <returns>The index of <paramref name="item"/> if found in this list; otherwise, -1.</returns>
		public int IndexOf(T item)
		{
			var comparer = EqualityComparer<T>.Default;
			int ret = 0;
			foreach (var sourceItem in this)
			{
				if (comparer.Equals(item, sourceItem))
					return ret;
				++ret;
			}

			return -1;
		}

		/// <summary>
		/// Adds an item to the end of this list.
		/// </summary>
		/// <param name="item">The object to add to this list.</param>
		/// <exception cref="T:System.NotSupportedException">
		/// This list is read-only.
		/// </exception>
		void ICollection<T>.Add(T item)
		{
			DoInsert(Count, item);
		}

		/// <summary>
		/// Determines whether this list contains a specific value.
		/// </summary>
		/// <param name="item">The object to locate in this list.</param>
		/// <returns>
		/// true if <paramref name="item"/> is found in this list; otherwise, false.
		/// </returns>
		bool ICollection<T>.Contains(T item)
		{
			var comparer = EqualityComparer<T>.Default;
			foreach (var entry in this)
			{
				if (comparer.Equals(item, entry))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Copies the elements of this list to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
		/// </summary>
		/// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from this slice. The <see cref="T:System.Array"/> must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
		/// <exception cref="T:System.ArgumentNullException">
		/// <paramref name="array"/> is null.
		/// </exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="arrayIndex"/> is less than 0.
		/// </exception>
		/// <exception cref="T:System.ArgumentException">
		/// <paramref name="arrayIndex"/> is equal to or greater than the length of <paramref name="array"/>.
		/// -or-
		/// The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.
		/// </exception>
		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));

			int count = Count;
			CheckRangeArguments(array.Length, arrayIndex, count);
			CopyToArray(array, arrayIndex);
		}

		/// <summary>
		/// Copies the deque elemens into an array. The resulting array always has all the deque elements contiguously.
		/// </summary>
		/// <param name="array">The destination array.</param>
		/// <param name="arrayIndex">The optional index in the destination array at which to begin writing.</param>
		private void CopyToArray(Array array, int arrayIndex = 0)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array));

			if (IsSplit)
			{
				// The existing buffer is split, so we have to copy it in parts
				int length = Capacity - _offset;
				Array.Copy(_buffer, _offset, array, arrayIndex, length);
				Array.Copy(_buffer, 0, array, arrayIndex + length, Count - length);
			}
			else
			{
				// The existing buffer is whole
				Array.Copy(_buffer, _offset, array, arrayIndex, Count);
			}
		}

		/// <summary>
		/// Removes the first occurrence of a specific object from this list.
		/// </summary>
		/// <param name="item">The object to remove from this list.</param>
		/// <returns>
		/// true if <paramref name="item"/> was successfully removed from this list; otherwise, false. This method also returns false if <paramref name="item"/> is not found in this list.
		/// </returns>
		/// <exception cref="T:System.NotSupportedException">
		/// This list is read-only.
		/// </exception>
		public bool Remove(T item)
		{
			int index = IndexOf(item);
			if (index == -1)
				return false;

			DoRemoveAt(index);
			return true;
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
		/// </returns>
		public IEnumerator<T> GetEnumerator()
		{
			int count = Count;
			for (int i = 0; i != count; ++i)
			{
				yield return DoGetItem(i);
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
		/// </returns>
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion
		#region ObjectListImplementations

		private static bool IsT(object value)
		{
			if (value is T)
				return true;
			if (value != null)
				return false;
			return default(T) == null;
		}

		int System.Collections.IList.Add(object value)
		{
			if (value == null && default(T) != null)
				throw new ArgumentNullException(nameof(value), "Value cannot be null.");
			if (!IsT(value))
				throw new ArgumentException("Value is of incorrect type.", nameof(value));
			AddToBack((T)value);
			return Count - 1;
		}

		bool System.Collections.IList.Contains(object value)
		{
			return IsT(value) ? ((ICollection<T>)this).Contains((T)value) : false;
		}

		int System.Collections.IList.IndexOf(object value)
		{
			return IsT(value) ? IndexOf((T)value) : -1;
		}

		void System.Collections.IList.Insert(int index, object value)
		{
			if (value == null && default(T) != null)
				throw new ArgumentNullException("value", "Value cannot be null.");
			if (!IsT(value))
				throw new ArgumentException("Value is of incorrect type.", "value");
			Insert(index, (T)value);
		}

		bool System.Collections.IList.IsFixedSize
		{
			get { return false; }
		}

		bool System.Collections.IList.IsReadOnly
		{
			get { return false; }
		}

		void System.Collections.IList.Remove(object value)
		{
			if (IsT(value))
				Remove((T)value);
		}

		object System.Collections.IList.this[int index]
		{
			get
			{
				return this[index];
			}

			set
			{
				if (value == null && default(T) != null)
					throw new ArgumentNullException(nameof(value), "Value cannot be null.");
				if (!IsT(value))
					throw new ArgumentException("Value is of incorrect type.", nameof(value));
				this[index] = (T)value;
			}
		}

		void System.Collections.ICollection.CopyTo(Array array, int index)
		{
			if (array == null)
				throw new ArgumentNullException(nameof(array), "Destination array cannot be null.");
			CheckRangeArguments(array.Length, index, Count);

			try
			{
				CopyToArray(array, index);
			}
			catch (ArrayTypeMismatchException ex)
			{
				throw new ArgumentException("Destination array is of incorrect type.", nameof(array), ex);
			}
			catch (RankException ex)
			{
				throw new ArgumentException("Destination array must be single dimensional.", nameof(array), ex);
			}
		}

		bool System.Collections.ICollection.IsSynchronized
		{
			get { return false; }
		}

		object System.Collections.ICollection.SyncRoot
		{
			get { return this; }
		}

		#endregion
		#region GenericListHelpers

		/// <summary>
		/// Checks the <paramref name="index"/> argument to see if it refers to a valid insertion point in a source of a given length.
		/// </summary>
		/// <param name="sourceLength">The length of the source. This parameter is not checked for validity.</param>
		/// <param name="index">The index into the source.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index to an insertion point for the source.</exception>
		private static void CheckNewIndexArgument(int sourceLength, int index)
		{
			if (index < 0 || index > sourceLength)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "Invalid new index " + index + " for source length " + sourceLength);
			}
		}

		/// <summary>
		/// Checks the <paramref name="index"/> argument to see if it refers to an existing element in a source of a given length.
		/// </summary>
		/// <param name="sourceLength">The length of the source. This parameter is not checked for validity.</param>
		/// <param name="index">The index into the source.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index to an existing element for the source.</exception>
		private static void CheckExistingIndexArgument(int sourceLength, int index)
		{
			if (index < 0 || index >= sourceLength)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "Invalid existing index " + index + " for source length " + sourceLength);
			}
		}

		/// <summary>
		/// Checks the <paramref name="offset"/> and <paramref name="count"/> arguments for validity when applied to a source of a given length. Allows 0-element ranges, including a 0-element range at the end of the source.
		/// </summary>
		/// <param name="sourceLength">The length of the source. This parameter is not checked for validity.</param>
		/// <param name="offset">The index into source at which the range begins.</param>
		/// <param name="count">The number of elements in the range.</param>
		/// <exception cref="ArgumentOutOfRangeException">Either <paramref name="offset"/> or <paramref name="count"/> is less than 0.</exception>
		/// <exception cref="ArgumentException">The range [offset, offset + count) is not within the range [0, sourceLength).</exception>
		private static void CheckRangeArguments(int sourceLength, int offset, int count)
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
		/// Gets a value indicating whether this instance is empty.
		/// </summary>
		private bool IsEmpty
		{
			get { return Count == 0; }
		}

		/// <summary>
		/// Gets a value indicating whether this instance is at full capacity.
		/// </summary>
		private bool IsFull
		{
			get { return Count == Capacity; }
		}

		/// <summary>
		/// Gets a value indicating whether the buffer is "split" (meaning the beginning of the view is at a later index in <see cref="_buffer"/> than the end).
		/// </summary>
		private bool IsSplit
		{
			get
			{
				// Overflow-safe version of "(offset + Count) > Capacity"
				return _offset > (Capacity - Count);
			}
		}

		/// <summary>
		/// Gets or sets the capacity for this deque. This value must always be greater than zero, and this property cannot be set to a value less than <see cref="Count"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException"><c>Capacity</c> cannot be set to a value less than <see cref="Count"/>.</exception>
		public int Capacity
		{
			get
			{
				return _buffer.Length;
			}

			set
			{
				if (value < Count)
					throw new ArgumentOutOfRangeException(nameof(value), "Capacity cannot be set to a value less than Count");

				if (value == _buffer.Length)
					return;

				// Create the new _buffer and copy our existing range.
				T[] newBuffer = new T[value];
				CopyToArray(newBuffer);

				// Set up to use the new _buffer.
				_buffer = newBuffer;
				_offset = 0;
			}
		}

		/// <summary>
		/// Gets the number of elements contained in this deque.
		/// </summary>
		/// <returns>The number of elements contained in this deque.</returns>
		public int Count { get; private set; }

		/// <summary>
		/// Applies the offset to <paramref name="index"/>, resulting in a buffer index.
		/// </summary>
		/// <param name="index">The deque index.</param>
		/// <returns>The buffer index.</returns>
		private int DequeIndexToBufferIndex(int index)
		{
			return (index + _offset) % Capacity;
		}

		/// <summary>
		/// Gets an element at the specified view index.
		/// </summary>
		/// <param name="index">The zero-based view index of the element to get. This index is guaranteed to be valid.</param>
		/// <returns>The element at the specified index.</returns>
		private T DoGetItem(int index)
		{
			return _buffer[DequeIndexToBufferIndex(index)];
		}

		/// <summary>
		/// Sets an element at the specified view index.
		/// </summary>
		/// <param name="index">The zero-based view index of the element to get. This index is guaranteed to be valid.</param>
		/// <param name="item">The element to store in the list.</param>
		private void DoSetItem(int index, T item)
		{
			_buffer[DequeIndexToBufferIndex(index)] = item;
		}

		/// <summary>
		/// Inserts an element at the specified view index.
		/// </summary>
		/// <param name="index">The zero-based view index at which the element should be inserted. This index is guaranteed to be valid.</param>
		/// <param name="item">The element to store in the list.</param>
		private void DoInsert(int index, T item)
		{
			EnsureCapacityForOneElement();

			if (index == 0)
			{
				DoAddToFront(item);
				return;
			}
			else if (index == Count)
			{
				DoAddToBack(item);
				return;
			}

			DoInsertRange(index, new[] { item });
		}

		/// <summary>
		/// Removes an element at the specified view index.
		/// </summary>
		/// <param name="index">The zero-based view index of the element to remove. This index is guaranteed to be valid.</param>
		private void DoRemoveAt(int index)
		{
			if (index == 0)
			{
				DoRemoveFromFront();
				return;
			}
			else if (index == Count - 1)
			{
				DoRemoveFromBack();
				return;
			}

			DoRemoveRange(index, 1);
		}

		/// <summary>
		/// Increments <see cref="_offset"/> by <paramref name="value"/> using modulo-<see cref="Capacity"/> arithmetic.
		/// </summary>
		/// <param name="value">The value by which to increase <see cref="_offset"/>. May not be negative.</param>
		/// <returns>The value of <see cref="_offset"/> after it was incremented.</returns>
		private int PostIncrement(int value)
		{
			int ret = _offset;
			_offset += value;
			_offset %= Capacity;
			return ret;
		}

		/// <summary>
		/// Decrements <see cref="_offset"/> by <paramref name="value"/> using modulo-<see cref="Capacity"/> arithmetic.
		/// </summary>
		/// <param name="value">The value by which to reduce <see cref="_offset"/>. May not be negative or greater than <see cref="Capacity"/>.</param>
		/// <returns>The value of <see cref="_offset"/> before it was decremented.</returns>
		private int PreDecrement(int value)
		{
			_offset -= value;
			if (_offset < 0)
				_offset += Capacity;
			return _offset;
		}

		/// <summary>
		/// Inserts a single element to the back of the view. <see cref="IsFull"/> must be false when this method is called.
		/// </summary>
		/// <param name="value">The element to insert.</param>
		private void DoAddToBack(T value)
		{
			_buffer[DequeIndexToBufferIndex(Count)] = value;
			++Count;
		}

		/// <summary>
		/// Inserts a single element to the front of the view. <see cref="IsFull"/> must be false when this method is called.
		/// </summary>
		/// <param name="value">The element to insert.</param>
		private void DoAddToFront(T value)
		{
			_buffer[PreDecrement(1)] = value;
			++Count;
		}

		/// <summary>
		/// Removes and returns the last element in the view. <see cref="IsEmpty"/> must be false when this method is called.
		/// </summary>
		/// <returns>The former last element.</returns>
		private T DoRemoveFromBack()
		{
			T ret = _buffer[DequeIndexToBufferIndex(Count - 1)];
			--Count;
			return ret;
		}

		/// <summary>
		/// Removes and returns the first element in the view. <see cref="IsEmpty"/> must be false when this method is called.
		/// </summary>
		/// <returns>The former first element.</returns>
		private T DoRemoveFromFront()
		{
			--Count;
			return _buffer[PostIncrement(1)];
		}

		/// <summary>
		/// Inserts a range of elements into the view.
		/// </summary>
		/// <param name="index">The index into the view at which the elements are to be inserted.</param>
		/// <param name="collection">The elements to insert. The sum of <c>collection.Count</c> and <see cref="Count"/> must be less than or equal to <see cref="Capacity"/>.</param>
		private void DoInsertRange(int index, IReadOnlyCollection<T> collection)
		{
			var collectionCount = collection.Count;
			// Make room in the existing list
			if (index < Count / 2)
			{
				// Inserting into the first half of the list

				// Move lower items down: [0, index) -> [Capacity - collectionCount, Capacity - collectionCount + index)
				// This clears out the low "index" number of items, moving them "collectionCount" places down;
				//   after rotation, there will be a "collectionCount"-sized hole at "index".
				int copyCount = index;
				int writeIndex = Capacity - collectionCount;
				for (int j = 0; j != copyCount; ++j)
					_buffer[DequeIndexToBufferIndex(writeIndex + j)] = _buffer[DequeIndexToBufferIndex(j)];

				// Rotate to the new view
				PreDecrement(collectionCount);
			}
			else
			{
				// Inserting into the second half of the list

				// Move higher items up: [index, count) -> [index + collectionCount, collectionCount + count)
				int copyCount = Count - index;
				int writeIndex = index + collectionCount;
				for (int j = copyCount - 1; j != -1; --j)
					_buffer[DequeIndexToBufferIndex(writeIndex + j)] = _buffer[DequeIndexToBufferIndex(index + j)];
			}

			// Copy new items into place
			int i = index;
			foreach (T item in collection)
			{
				_buffer[DequeIndexToBufferIndex(i)] = item;
				++i;
			}

			// Adjust valid count
			Count += collectionCount;
		}

		/// <summary>
		/// Removes a range of elements from the view.
		/// </summary>
		/// <param name="index">The index into the view at which the range begins.</param>
		/// <param name="collectionCount">The number of elements in the range. This must be greater than 0 and less than or equal to <see cref="Count"/>.</param>
		private void DoRemoveRange(int index, int collectionCount)
		{
			if (index == 0)
			{
				// Removing from the beginning: rotate to the new view
				PostIncrement(collectionCount);
				Count -= collectionCount;
				return;
			}
			else if (index == Count - collectionCount)
			{
				// Removing from the ending: trim the existing view
				Count -= collectionCount;
				return;
			}

			if ((index + (collectionCount / 2)) < Count / 2)
			{
				// Removing from first half of list

				// Move lower items up: [0, index) -> [collectionCount, collectionCount + index)
				int copyCount = index;
				int writeIndex = collectionCount;
				for (int j = copyCount - 1; j != -1; --j)
					_buffer[DequeIndexToBufferIndex(writeIndex + j)] = _buffer[DequeIndexToBufferIndex(j)];

				// Rotate to new view
				PostIncrement(collectionCount);
			}
			else
			{
				// Removing from second half of list

				// Move higher items down: [index + collectionCount, count) -> [index, count - collectionCount)
				int copyCount = Count - collectionCount - index;
				int readIndex = index + collectionCount;
				for (int j = 0; j != copyCount; ++j)
					_buffer[DequeIndexToBufferIndex(index + j)] = _buffer[DequeIndexToBufferIndex(readIndex + j)];
			}

			// Adjust valid count
			Count -= collectionCount;
		}

		/// <summary>
		/// Doubles the capacity if necessary to make room for one more element. When this method returns, <see cref="IsFull"/> is false.
		/// </summary>
		private void EnsureCapacityForOneElement()
		{
			if (IsFull)
			{
				Capacity = (Capacity == 0) ? 1 : Capacity * 2;
			}
		}

		/// <summary>
		/// Inserts a single element at the back of this deque.
		/// </summary>
		/// <param name="value">The element to insert.</param>
		public void AddToBack(T value)
		{
			EnsureCapacityForOneElement();
			DoAddToBack(value);
		}

		/// <summary>
		/// Inserts a single element at the front of this deque.
		/// </summary>
		/// <param name="value">The element to insert.</param>
		public void AddToFront(T value)
		{
			EnsureCapacityForOneElement();
			DoAddToFront(value);
		}

		/// <summary>
		/// Inserts a collection of elements into this deque.
		/// </summary>
		/// <param name="index">The index at which the collection is inserted.</param>
		/// <param name="collection">The collection of elements to insert.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index to an insertion point for the source.</exception>
		public void InsertRange(int index, IEnumerable<T> collection)
		{
			CheckNewIndexArgument(Count, index);
			var source = CollectionHelpers.ReifyCollection(collection);
			int collectionCount = source.Count;

			// Overflow-safe check for "Count + collectionCount > Capacity"
			if (collectionCount > Capacity - Count)
			{
				Capacity = checked(Count + collectionCount);
			}

			if (collectionCount == 0)
			{
				return;
			}

			DoInsertRange(index, source);
		}

		/// <summary>
		/// Removes a range of elements from this deque.
		/// </summary>
		/// <param name="offset">The index into the deque at which the range begins.</param>
		/// <param name="count">The number of elements to remove.</param>
		/// <exception cref="ArgumentOutOfRangeException">Either <paramref name="offset"/> or <paramref name="count"/> is less than 0.</exception>
		/// <exception cref="ArgumentException">The range [<paramref name="offset"/>, <paramref name="offset"/> + <paramref name="count"/>) is not within the range [0, <see cref="Count"/>).</exception>
		public void RemoveRange(int offset, int count)
		{
			CheckRangeArguments(Count, offset, count);

			if (count == 0)
			{
				return;
			}

			DoRemoveRange(offset, count);
		}

		/// <summary>
		/// Removes and returns the last element of this deque.
		/// </summary>
		/// <returns>The former last element.</returns>
		/// <exception cref="InvalidOperationException">The deque is empty.</exception>
		public T RemoveFromBack()
		{
			if (IsEmpty)
				throw new InvalidOperationException("The deque is empty.");

			return DoRemoveFromBack();
		}

		/// <summary>
		/// Removes and returns the first element of this deque.
		/// </summary>
		/// <returns>The former first element.</returns>
		/// <exception cref="InvalidOperationException">The deque is empty.</exception>
		public T RemoveFromFront()
		{
			if (IsEmpty)
				throw new InvalidOperationException("The deque is empty.");

			return DoRemoveFromFront();
		}

		/// <summary>
		/// Removes all items from this deque.
		/// </summary>
		public void Clear()
		{
			_offset = 0;
			Count = 0;
		}

		/// <summary>
		/// Creates and returns a new array containing the elements in this deque.
		/// </summary>
		public T[] ToArray()
		{
			var result = new T[Count];
			((ICollection<T>)this).CopyTo(result, 0);
			return result;
		}

		private sealed class DebugView
		{
			private readonly Deque<T> deque;

			public DebugView(Deque<T> deque)
			{
				this.deque = deque;
			}

			public T[] Items
			{
				get
				{
					return deque.ToArray();
				}
			}
		}
	}

	internal static class CollectionHelpers
	{
		public static IReadOnlyCollection<T> ReifyCollection<T>(IEnumerable<T> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var result = source as IReadOnlyCollection<T>;
			if (result != null)
				return result;
			var collection = source as ICollection<T>;
			if (collection != null)
				return new CollectionWrapper<T>(collection);
			var nongenericCollection = source as ICollection;
			if (nongenericCollection != null)
				return new NongenericCollectionWrapper<T>(nongenericCollection);

			return new List<T>(source);
		}

		private sealed class NongenericCollectionWrapper<T> : IReadOnlyCollection<T>
		{
			private readonly ICollection _collection;

			public NongenericCollectionWrapper(ICollection collection)
			{
				if (collection == null)
					throw new ArgumentNullException(nameof(collection));
				_collection = collection;
			}

			public int Count
			{
				get
				{
					return _collection.Count;
				}
			}

			public IEnumerator<T> GetEnumerator()
			{
				foreach (T item in _collection)
					yield return item;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return _collection.GetEnumerator();
			}
		}

		private sealed class CollectionWrapper<T> : IReadOnlyCollection<T>
		{
			private readonly ICollection<T> _collection;

			public CollectionWrapper(ICollection<T> collection)
			{
				if (collection == null)
					throw new ArgumentNullException(nameof(collection));
				_collection = collection;
			}

			public int Count
			{
				get
				{
					return _collection.Count;
				}
			}

			public IEnumerator<T> GetEnumerator()
			{
				return _collection.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return _collection.GetEnumerator();
			}
		}
	}
}
