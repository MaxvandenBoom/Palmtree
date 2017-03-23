﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.helpers {

    class RingBuffer {


        private double[] mData = null;
        private uint mCursor = 0;
        private bool mWrapped = false;

        public RingBuffer(uint inSize) {
            mData = new double[inSize];     // values are automatically initiated to 0
            mCursor = 0;
            mWrapped = false;
        }

	    public bool IsFull() {
            return mWrapped; 
        }

        public uint CursorPos() {
            return mCursor;
        }

        public uint Fill() {
            return mWrapped ? (uint)mData.Count() : mCursor;
        }

        public double[] Data() {
            return mData;
        }

        public void Put(double inData) {

            if (mData.Count() > 0)    mData[mCursor] = inData;
            if (++mCursor == mData.Count()) {
                mWrapped = true;
                mCursor = 0;
            }
        }

    }

}
