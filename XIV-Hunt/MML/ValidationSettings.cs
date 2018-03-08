#region License
// The MIT License (MIT)
// 
// Copyright (c) 2014 Emma 'Eniko' Maassen
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
#endregion
using System;

namespace TextPlayer {
    public abstract class ValidationSettings {
        private int maxSize = 8192;
        private TimeSpan maxDuration = TimeSpan.FromMinutes(5);
        private byte minTempo = 32;
        private byte maxTempo = byte.MaxValue;
        private byte minOctave = 1;
        private byte maxOctave = 10;

        public ValidationSettings() {
        }

        /// <summary>
        /// Maximum allowed file size in bytes.
        /// </summary>
        public int MaxSize { get { return maxSize; } set { maxSize = value; } }
        /// <summary>
        /// Maximum allowed length of song.
        /// </summary>
        public TimeSpan MaxDuration { get { return maxDuration; } set { maxDuration = value; } }
        /// <summary>
        /// Minimum beats per minute.
        /// </summary>
        public byte MinTempo { get { return minTempo; } set { minTempo = value; } }
        /// <summary>
        /// Maximum beats per minute.
        /// </summary>
        public byte MaxTempo { get { return maxTempo; } set { maxTempo = value; } }
        public byte MinOctave { get { return minOctave; } set { minOctave = value; } }
        public byte MaxOctave { get { return maxOctave; } set { maxOctave = value; } }
    }
}
