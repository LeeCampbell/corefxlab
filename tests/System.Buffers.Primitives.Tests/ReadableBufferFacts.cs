// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Collections.Sequences;
using System.IO.Pipelines.Testing;
using System.Linq;
using Xunit;

namespace System.IO.Pipelines.Tests
{
    public abstract class ReadableBufferFacts
    {
        public class Array: ReadableBufferFacts
        {
            public Array() : base(ReadOnlyBufferFactory.Array) { }
        }

        public class OwnedMemory: ReadableBufferFacts
        {
            public OwnedMemory() : base(ReadOnlyBufferFactory.OwnedMemory) { }
        }

        public class SingleSegment: ReadableBufferFacts
        {
            public SingleSegment() : base(ReadOnlyBufferFactory.SingleSegment) { }
        }

        public class SegmentPerByte: ReadableBufferFacts
        {
            public SegmentPerByte() : base(ReadOnlyBufferFactory.SegmentPerByte) { }

            [Fact]
            // This test verifies that optimization for known cursors works and
            // avoids additional walk but it's only valid for multi segmented buffers
            public void ReadCursorSeekDoesNotCheckEndIfTrustingEnd()
            {
                var buffer = Factory.CreateOfSize(3);
                var buffer2 = Factory.CreateOfSize(3);
                ReadOnlyBuffer.Seek(buffer.Start, buffer2.End, 2, false);
            }
        }

        internal ReadOnlyBufferFactory Factory { get; }

        internal ReadableBufferFacts(ReadOnlyBufferFactory factory)
        {
            Factory = factory;
        }

        [Fact]
        public void EmptyIsCorrect()
        {
            var buffer = Factory.CreateOfSize(0);
            Assert.Equal(0, buffer.Length);
            Assert.True(buffer.IsEmpty);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        public void LengthIsCorrect(int length)
        {
            var buffer = Factory.CreateOfSize(length);
            Assert.Equal(length, buffer.Length);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        public void ToArrayIsCorrect(int length)
        {
            var data = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
            var buffer = Factory.CreateWithContent(data);
            Assert.Equal(length, buffer.Length);
            Assert.Equal(data, buffer.ToArray());
        }

        [Theory]
        [MemberData(nameof(OutOfRangeSliceCases))]
        public void ReadableBufferDoesNotAllowSlicingOutOfRange(Action<ReadOnlyBuffer> fail)
        {
            var buffer = Factory.CreateOfSize(100);
            var ex = Assert.Throws<InvalidOperationException>(() => fail(buffer));
        }

        [Fact]
        public void ReadableBufferMove_MovesReadCursor()
        {
            var buffer = Factory.CreateOfSize(100);
            var cursor = buffer.Move(buffer.Start, 65);
            Assert.Equal(buffer.Slice(65).Start, cursor);
        }

        [Fact]
        public void ReadableBufferMove_ChecksBounds()
        {
            var buffer = Factory.CreateOfSize(100);
            Assert.Throws<InvalidOperationException>(() => buffer.Move(buffer.Start, 101));
        }

        [Fact]
        public void ReadableBufferMove_DoesNotAlowNegative()
        {
            var buffer = Factory.CreateOfSize(20);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Move(buffer.Start, -1));
        }

        [Fact]
        public void ReadCursorSeekChecksEndIfNotTrustingEnd()
        {
            var buffer = Factory.CreateOfSize(3);
            var buffer2 = Factory.CreateOfSize(3);
            Assert.Throws<InvalidOperationException>(() => ReadOnlyBuffer.Seek(buffer.Start, buffer2.End, 2, true));
        }

        [Fact]
        public void SegmentStartIsConsideredInBoundsCheck()
        {
            // 0               50           100    0             50             100
            // [                ##############] -> [##############                ]
            //                         ^c1            ^c2
            var bufferSegment1 = new BufferSegment();
            bufferSegment1.SetMemory(new OwnedArray<byte>(new byte[100]), 50, 99);

            var bufferSegment2 = new BufferSegment();
            bufferSegment2.SetMemory(new OwnedArray<byte>(new byte[100]), 0, 50);
            bufferSegment1.SetNext(bufferSegment2);

            var readableBuffer = new ReadOnlyBuffer(new Position(bufferSegment1, 0), new Position(bufferSegment2, 50));

            var c1 = readableBuffer.Move(readableBuffer.Start, 25); // segment 1 index 75
            var c2 = readableBuffer.Move(readableBuffer.Start, 55); // segment 2 index 5

            var sliced = readableBuffer.Slice(c1, c2);

            Assert.Equal(30, sliced.Length);
        }

        [Fact]
        public void MovePrefersNextSegment()
        {
            var bufferSegment1 = new BufferSegment();
            bufferSegment1.SetMemory(new OwnedArray<byte>(new byte[100]), 49, 99);

            var bufferSegment2 = new BufferSegment();
            bufferSegment2.SetMemory(new OwnedArray<byte>(new byte[100]), 0, 0);
            bufferSegment1.SetNext(bufferSegment2);

            var readableBuffer = new ReadOnlyBuffer(new Position(bufferSegment1, 0), new Position(bufferSegment2, 0));

            var c1 = readableBuffer.Move(readableBuffer.Start, 50);

            Assert.Equal(0, c1.Index);
            Assert.Equal(bufferSegment2, c1.Segment);
        }

        [Fact]
        public void Create_WorksWithArray()
        {
            var readableBuffer = new ReadOnlyBuffer(new byte[] {1, 2, 3, 4, 5}, 2, 3);
            Assert.Equal(readableBuffer.ToArray(), new byte[] {3, 4, 5});
        }

        [Fact]
        public void Create_WorksWithOwnedMemory()
        {
            var memory = new OwnedArray<byte>(new byte[] {1, 2, 3, 4, 5});
            var readableBuffer = new ReadOnlyBuffer(memory, 2, 3);
            Assert.Equal(new byte[] {3, 4, 5}, readableBuffer.ToArray());
        }

        public static TheoryData<Action<ReadOnlyBuffer>> OutOfRangeSliceCases => new TheoryData<Action<ReadOnlyBuffer>>
        {
            b => b.Slice(101),
            b => b.Slice(0, 101),
            b => b.Slice(b.Start, 101),
            b => b.Slice(0, 70).Slice(b.End, b.End),
            b => b.Slice(0, 70).Slice(b.Start, b.End),
            b => b.Slice(0, 70).Slice(0, b.End),
            b => b.Slice(70, b.Start)
        };
    }
}
