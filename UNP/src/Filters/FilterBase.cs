﻿using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class FilterBase {

        protected string filterName = "";
        protected Logger logger = null;
        protected Parameters parameters = null;

        protected bool mEnableFilter = false;                   // Filter is enabled or disabled
        protected bool mLogDataStreams = false;                 // stores whether the initial configuration has the logging of data streams enabled or disabled
        protected bool mLogDataStreamsRuntime = false;          // stores whether during runtime the data streams should be logged (if it was on, then it can be switched off, resulting in 0's being logged)
        protected bool mAllowDataVisualization = false;         // stores whether data visualization is enabled or disabled. This is a local copy of the setting from Globals (originating from the Data class), set during configuration of the filter

        protected uint inputChannels = 0;
        protected uint outputChannels = 0;

        public string getName() {
            return filterName;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public void configureInputLogging(string prefix, SampleFormat input) {

            // retrieve and prepare the visualization of streams
            mAllowDataVisualization = Globals.getValue<bool>("AllowDataVisualization");
            if (mAllowDataVisualization) {

                // register the streams to visualize
                for (int i = 0; i < outputChannels; i++)
                    Data.RegisterVisualizationStream((prefix + "Input_Ch" + (i + 1)), input);

            }

            // retrieve and prepare the logging of streams
            mLogDataStreams = parameters.getValue<bool>("LogDataStreams");
            mLogDataStreamsRuntime = mLogDataStreams;
            if (mLogDataStreams) {

                // register the streams
                for (int i = 0; i < outputChannels; i++)
                    Data.RegisterSampleStream((prefix + "Input_Ch" + (i + 1)), input);

            }

        }

        // standard function to process the input loggin
        public void processInputLogging(double[] input) {

            // check if the streams should be logged to a file (initial setting) or visualization is allowed 
            if (mLogDataStreams || mAllowDataVisualization) {

                for (uint channel = 0; channel < inputChannels; ++channel) {

                    // check if the logging of streams is needed/allowed during runtime
                    if (mLogDataStreamsRuntime) {
                        // enabled initially and at runtime

                        // log values
                        Data.LogStreamValue(input[channel]);

                    } else {
                        // enabled initially but not at runtime

                        // log zeros
                        Data.LogStreamValue(0.0);

                    }

                    // check if the data should be visualized
                    if (mAllowDataVisualization) {

                        // log values
                        Data.LogVisualizationStreamValue(input[channel]);

                    }

                }   // end for loop

            }

        }   // end processInputLogging


    }

}
