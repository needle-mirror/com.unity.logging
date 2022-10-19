using System.Diagnostics;
using UnityEngine.Assertions;

namespace Unity.Logging
{
    /// <summary>
    /// Structure used in parsing strings. Contains offset and length.
    /// </summary>
    [DebuggerDisplay("Offset = {Offset}, Length = {Length}")]
    public struct ParseSegment
    {
        /// <summary>
        /// Start of the segment
        /// </summary>
        public int Offset;
        /// <summary>
        /// Length of the segment
        /// </summary>
        public int Length;

        /// <summary>
        /// Is this segment contains anything, or was initialized
        /// </summary>
        public bool IsValid => Offset >= 0 && Length >= 0;

        /// <summary>
        /// End of the segment
        /// </summary>
        public int OffsetEnd => Offset + Length;

        /// <summary>
        /// Reduce the ParseSegment by amount of bytes from the left and right
        /// </summary>
        /// <param name="origin">Original segment</param>
        /// <param name="bytesFromLeft">Amount to bytes to reduce from the left</param>
        /// <param name="bytesFromRight">Amount to bytes to reduce from the right</param>
        /// <returns>New reduced segment</returns>
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

        /// <summary>
        /// Cut segment from the left
        /// </summary>
        /// <param name="origin">Original segment</param>
        /// <param name="splitPoint">Split point inside the segment</param>
        /// <returns>New segment [origin.Offset..splitPoint)</returns>
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

        /// <summary>
        /// Cut segment from the right
        /// </summary>
        /// <param name="origin">Original segment</param>
        /// <param name="splitPoint">Split point inside the segment</param>
        /// <returns>New segment [splitPoint..original_end]</returns>
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

        /// <summary>
        /// Create local segment in parent's space
        /// </summary>
        /// <param name="parent">Parent</param>
        /// <param name="convertThis">Segment to convert</param>
        /// <returns>Same as convertThis, but offset is in parent's offset space</returns>
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
