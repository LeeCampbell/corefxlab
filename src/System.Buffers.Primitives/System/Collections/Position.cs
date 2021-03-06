﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Collections.Sequences
{
    public readonly struct Position : IEquatable<Position>
    {
        readonly object _segment;
        readonly int _index;

        public Position(object segment, int index)
        {
            _segment = segment;
            _index = index;
        }

        public Position(object segment)
        {
            _segment = segment;
            _index = 0;
        }

        public object Segment => _segment;
        public int Index => _index;

        public static explicit operator int(Position position) => position._index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (T segment, int index) Get<T>()
        {
            var segment = _segment == null ? default : (T)_segment;
            return (segment, _index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetSegment<T>()
        {
            switch (Segment)
            {
                case null:
                    return default;
                case T segment:
                    return segment;
            }

            ThrowHelper.ThrowInvalidOperationException(ExceptionResource.UnexpectedSegmentType);
            return default;
        }

        public static bool operator ==(Position left, Position right) => left._index == right._index && left._segment == right._segment;
        public static bool operator !=(Position left, Position right) => left._index != right._index || left._segment != right._segment;

        public static Position operator +(Position value, int index)
            => new Position(value._segment, value._index + index);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Equals(Position position) => this == position;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) =>
            obj is Position ? this == (Position)obj : false;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            var h1 = _segment?.GetHashCode() ?? 0;
            var h2 = _index.GetHashCode();

            var shift5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)shift5 + h1) ^ h2;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString() =>
            this == default ? "(default)" : _segment == null ? $"{_index}" : $"{_segment}[{_index}]";
    }
}
