using System.Diagnostics;
using UnityEngine.Assertions;

namespace Unity.Logging
{
    /// <summary>
    /// Structure used in parsing strings. Conains offset and length.
    /// </summary>
    [DebuggerDisplay("Offset = {Offset}, Length = {Length}")]
    public struct ParseSegment
    {
        public int Offset;
        public int Length;

        public bool IsValid => Offset >= 0 && Length >= 0;
        public int OffsetEnd => Offset + Length;

        public static ParseSegment Reduce(in ParseSegment origin, int bytesFromLeft, int bytesFromRight)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsTrue(origin.IsValid);
#endif
            return new ParseSegment {
                Offset = origin.Offset + bytesFromLeft,
                Length = origin.Length - bytesFromLeft - bytesFromRight
            };
        }

        public static ParseSegment LeftPart(in ParseSegment origin, int splitPoint)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsTrue(origin.IsValid);
            Assert.IsTrue(splitPoint >= origin.Offset);
            Assert.IsTrue(splitPoint < origin.Offset + origin.Length);
#endif
            return new ParseSegment {
                Offset = origin.Offset,
                Length = splitPoint - origin.Offset
            };
        }

        public static ParseSegment RightPart(in ParseSegment origin, int splitPoint)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsTrue(origin.IsValid);
            Assert.IsTrue(splitPoint >= origin.Offset);
            Assert.IsTrue(splitPoint < origin.Offset + origin.Length);
#endif
            return new ParseSegment {
                Offset = splitPoint,
                Length = origin.Length - (splitPoint - origin.Offset)
            };
        }

        public static ParseSegment Local(in ParseSegment parent, in ParseSegment convertThis)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsTrue(parent.IsValid);
            Assert.IsTrue(convertThis.IsValid);
#endif
            return new ParseSegment {
                Offset = convertThis.Offset - parent.Offset,
                Length = convertThis.Length
            };
        }
    }
}
