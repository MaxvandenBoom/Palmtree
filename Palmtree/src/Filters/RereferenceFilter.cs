﻿/**
 * Rereference Filter class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using Palmtree.Core;
using Palmtree.Core.Params;
using System;
using System.Collections.Generic;

namespace Palmtree.Filters {

    /// <summary>
    /// RereferenceFilter class
    /// 
    /// ...
    /// </summary>
    public class RereferenceFilter : FilterBase, IFilter {

        private new const int CLASS_VERSION = 1;
        
        private int method = -1;										            // (configuration) re-referencing method (0=off, 1=CAR)
        private int[] excludeChannels = new int[0];				            // (configuration) channels (1-based) to exclude from re-referencing
        private int[] includedChannels = null;				                // channels (0-based) that are included in re-referencing, helper variable

        public RereferenceFilter(string filterName) {

            // set class version
            base.CLASS_VERSION = CLASS_VERSION;

            // store the filter name
            this.filterName = filterName;

            // initialize the logger and parameters with the filter name
            logger = LogManager.GetLogger(filterName);
            parameters = ParameterManager.GetParameters(filterName, Parameters.ParamSetTypes.Filter);

            // define the parameters
            parameters.addParameter <bool>  (
                "EnableFilter",
                "Enable Re-Reference Filter",
                "1");

            parameters.addParameter<bool>(
                "LogDataStreams",
                "Log the filter's intermediate and output data streams. See 'Data' tab for more settings on sample stream logging.",
                "0");

            parameters.addParameter<int>(
                "Method",
                "The method used to re-reference the incoming signals.",
                "0", "0", "0", new string[] { "Common Average Re-referencing (CAR)"});

            parameters.addParameter<int[]>(
                "ExcludeChannels",
                "The channels (1-based indexing: 1 to 128) to exclude from the re-reference calculation.\n\nNote: these channels are still re-referenced and passed through as output",
                "0", "", "");

            // message
            logger.Info("Filter created (version " + CLASS_VERSION + ")");

        }
        
        /**
         * Configure the filter. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         * (initialization of the filter is done later by the initialize function)
         **/
        public bool configure(ref SamplePackageFormat input, out SamplePackageFormat output) {

            // check sample-major ordered input
            if (input.valueOrder != SamplePackageFormat.ValueOrder.SampleMajor) {
                logger.Error("This filter is designed to work only with sample-major ordered input");
                output = null;
                return false;
            }

            // retrieve the number of input channels
            if (input.numChannels <= 0) {
                logger.Error("Number of input channels cannot be 0");
                output = null;
                return false;
            }

            // the output package will be in the same format as the input package
            output = new SamplePackageFormat(input.numChannels, input.numSamples, input.packageRate, input.valueOrder);

            // store a references to the input and output format
            inputFormat = input;
            outputFormat = output;
            
            // check the values and application logic of the parameters
            if (!checkParameters(parameters))   return false;

            // transfer the parameters to local variables
            transferParameters(parameters);

            // configure output logging for this filter
            configureOutputLogging(filterName + "_", output);

            // print configuration
            printLocalConfiguration();

            // return success
            return true;

        }

        /// <summary>Re-configure and/or reset the configration parameters of the filter (defined in the newParameters argument) on-the-fly.</summary>
        /// <param name="newParameters">Parameter object that defines the configuration parameters to be set. Set to NULL to leave the configuration parameters untouched.</param>
        /// <param name="resetOption">Filter reset options. 0 will reset the minimum; 1 will perform a complete reset of filter information. > 1 for custom resets.</param>
        /// <returns>A boolean, either true for a succesfull re-configuration, or false upon failure</returns>
        public bool configureRunningFilter(Parameters newParameters, int resetOption) {
            
            // check if new parameters are given (only a reset is also an option)
            if (newParameters != null) {

                //
                // no pre-check on the number of output channels is needed here, the number of output
                // channels will remain the some regardless to the filter being enabled or disabled
                // 

                // check the values and application logic of the parameters
                if (!checkParameters(newParameters)) return false;

                // retrieve and check the LogDataStreams parameter
                bool newLogDataStreams = newParameters.getValue<bool>("LogDataStreams");
                if (!mLogDataStreams && newLogDataStreams) {
                    // logging was (in the initial configuration) switched off and is trying to be switched on
                    // (refuse, it cannot be switched on, because sample streams have to be registered during the first configuration)

                    // message
                    logger.Error("Cannot switch the logging of data streams on because it was initially switched off (and streams need to be registered during the first configuration, logging is refused");

                    // return failure
                    return false;

                }

                // transfer the parameters to local variables
                transferParameters(newParameters);

                // apply change in the logging of sample streams
                if (mLogDataStreams && mLogDataStreamsRuntime && !newLogDataStreams) {
                    // logging was (in the initial configuration) switched on and is currently on but wants to be switched off (resulting in 0's being output)

                    // message
                    logger.Debug("Logging of data streams was switched on but is now switched off, only zeros will be logged");

                    // switch logging off (to zeros)
                    mLogDataStreamsRuntime = false;

                } else if (mLogDataStreams && !mLogDataStreamsRuntime && newLogDataStreams) {
                    // logging was (in the initial configuration) switched on and is currently off but wants to be switched on (resume logging)

                    // message
                    logger.Debug("Logging of data streams was switched off but is now switched on, logging is resumed");

                    // switch logging on
                    mLogDataStreamsRuntime = true;

                }

                // print configuration
                printLocalConfiguration();

            }

            // TODO: take resetFilter into account (currently always resets the buffers on initialize
            //          but when set not to reset, the buffers should be resized while retaining their values!)
            

            // initialize the variables
            initialize();

            // return success
            return true;

        }


        /**
         * check the values and application logic of the given parameter set
         **/
        private bool checkParameters(Parameters newParameters) {

            // TODO: parameters.checkminimum, checkmaximum

            // filter is enabled/disabled
            bool newEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (newEnableFilter) {
                
                // check the re-referencing method
                int newMethod = parameters.getValue<int>("Method");
                if (newMethod != 0) {
                    logger.Error("The Method parameter has an unknown value");
                    return false;
                }

                // check the exclude channels
                int[] newExcludeChannels = parameters.getValue<int[]>("ExcludeChannels");
                for (int i = 0; i < newExcludeChannels.Length; i++) {
                    if (newExcludeChannels[i] < 1 || newExcludeChannels[i] > inputFormat.numChannels) {
                        logger.Error("The RerefencingExcludeChannels parameter contains a channel-index (" + newExcludeChannels[i] + ") that exceeds the number of incoming channels (should be between 1 and " + inputFormat.numChannels + ")");
                        return false;
                    }
                    for (int j = 0; j < newExcludeChannels.Length; j++) {
                        if (i != j && newExcludeChannels[i] == newExcludeChannels[j]) {
                            logger.Error("The RerefencingExcludeChannels parameter contains the same channel (" + newExcludeChannels[i] + ") more than once");
                            return false;
                        }
                    }
                }

                // ensure not all channels are excluded
                if (newExcludeChannels.Length == inputFormat.numChannels) {
                    logger.Error("All channels are excluded from re-referencing, make sure at least one channel remains included");
                    return false;
                }

            }

            // return success
            return true;

        }

        /**
         * transfer the given parameter set to local variables
         **/
        private void transferParameters(Parameters newParameters) {

            // filter is enabled/disabled
            mEnableFilter = newParameters.getValue<bool>("EnableFilter");

            // check if the filter is enabled
            if (mEnableFilter) {

                // retrieve the re-referencing method
                method = parameters.getValue<int>("Method");

                // retrieve the re-referencing excluded channels
                excludeChannels = parameters.getValue<int[]>("ExcludeChannels");

            }

        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Filter configuration: " + filterName + " ---");
            logger.Debug("Input channels: " + inputFormat.numChannels);
            logger.Debug("Enabled: " + mEnableFilter);
            // TODO: 
            logger.Debug("Output channels: " + outputFormat.numChannels);
            if (mEnableFilter) {

            }

        }

        public bool initialize() {

            // check if the filter is enabled
            if (mEnableFilter) {


                // create a array of channels that are included
                List<int> lstIncludedChannels = new List<int>();
                for (int i = 0; i < inputFormat.numChannels; i++) {
                    bool isFound = false;
                    for (int j = 0; j < excludeChannels.Length; j++) {
                        if ((i + 1) == excludeChannels[j]) {
                            isFound = true;
                            break;
                        }
                    }
                    if (!isFound)
                        lstIncludedChannels.Add(i);    // deliberately 0-based here 
                }
                includedChannels = lstIncludedChannels.ToArray();
            }
            
            // return success
            return true;

        }

        public void start() {
            
        }

        public void stop() {

        }

        public bool isStarted() {
            return false;
        }

        public void process(double[] input, out double[] output) {
            
            // check if the filter is enabled
            if (mEnableFilter) {
                // filter enabled

                // create the output package (no changes to #channels in this filter, so #output-samples is same as actual #input-samples)
                output = new double[input.Length];
            
                // loop over samples (by sets of channels)
                for (int iSample = 0; iSample < input.Length; iSample += inputFormat.numChannels) {
                    double rerefValue = 0;

                    // loop over the channels (that are included for re-referencing) and add those to the total
                    for (int iRefChan = 0; iRefChan < includedChannels.Length; iRefChan++)
                        rerefValue += input[iSample + includedChannels[iRefChan]];

                    // calculate the average
                    rerefValue /= includedChannels.Length;
                                                        
                    // loop over all the channels in the sample and subtract the average
                    for (int iChan = 0; iChan < inputFormat.numChannels; iChan++)
                        output[iSample + iChan] = input[iSample + iChan] - rerefValue;

                }   
    
            } else {
                // filter disabled

                // pass reference
                output = input;

            }

            // handle the data logging of the output (both to file and for visualization)
            processOutputLogging(output);
            
        }
        
        public void destroy() {

            // stop the filter
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

        }

    }


}