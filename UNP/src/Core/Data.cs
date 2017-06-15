﻿using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UNP.Core.Events;
using UNP.Core.Helpers;
using UNP.Core.Params;


namespace UNP.Core {
    
    // Data class. Takes care of data storage and visualization
    // 
    // 
    // static class over singleton pattern because we do not need an instance using an interface
    // or be passed around, also the static will be stored in stack (instead of heap) giving better
    // performance (important since the Data class is called upon frequently)
    public static class Data {

        private static Logger logger = LogManager.GetLogger("Data");
        private static Parameters parameters = ParameterManager.GetParameters("Data", Parameters.ParamSetTypes.Data);

        private static bool mLogSourceInput = false;                                            // source input logging enabled/disabled (by configuration parameter)
        private static bool mLogSourceInputRuntime = false;                                     // stores whether during runtime the source input should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)
        private static bool mLogDataStreams = false;                                            // stream logging enabled/disabled (by configuration parameter)
        private static bool mLogDataStreamsRuntime = false;                                     // stores whether during runtime the streams should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)
        private static bool mLogEvents = false;                                                 // 
        private static bool mLogEventsRuntime = false;                                          // stores whether during runtime the events should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)

        private static bool mAllowDataVisualization = false;                                    // data visualization enabled/disabled

        // source streams
        private static int numSourceInputStreams = 0;                                           // the number of streams coming from the source input
        private static List<string> registeredSourceInputStreamNames = new List<string>(0);     // the names of the registered source input streams to store in the .src file
        private static List<int> registeredSourceInputStreamTypes = new List<int>(0);           // the types of the registered source input streams to store in the .src file
        private static FileStream sourceStream = null;                                          // filestream that is fed to the binarywriter, containing the stream of values to be written to the .src file
        private static BinaryWriter sourceStreamWriter = null;                                  // writer that writes values to the .src file
        private static uint sourceSampleCounter = 0;                                            // the current row of values being written to the .src file, acts as id
        private static Stopwatch sourceStopWatch = new Stopwatch();                             // creates stopwatch to measure time difference between incoming samples
        private static double sourceElapsedTime = 0;                                            // amount of time [ms] elapsed since start of proccesing of previous sample

        // datastreams
        private static int numDataStreams = 0;                                                  // the total number of data streams to be logged in the .dat file
        private static List<string> registeredDataStreamNames = new List<string>(0);            // the names of the registered streams to store in the .dat file
        private static List<int> registeredDataStreamTypes = new List<int>(0);                  // the types of the registered streams to store in the .dat file
        private static FileStream dataStream = null;                                            // filestream that is fed to the binarywriter, containing the stream of values to be written to the .dat file
        private static BinaryWriter dataStreamWriter = null;                                    // writer that writes values to the .dat file
        private static double[] dataStreamValues = null;                                        // holds the values of all data streams that are registered to be logged 
        private static uint dataSampleCounter = 0;                                              // the current row of values being written to the .dat file, acts as id
        private static int dataValuePointer = 0;                                                // the current location in the dataStreamValues array that the incoming value is written to
        private static Stopwatch dataStopWatch = new Stopwatch();                               // creates stopwatch to measure time difference between incoming samples
        private static double dataElapsedTime = 0;                                              // amount of time [ms] elapsed since start of proccesing of previous sample

        // visualization
        private static int numVisualizationStreams = 0;                                         // the total number of streams to visualize
        private static List<string> registeredVisualizationStreamNames = new List<string>(0);   // the names of the registered streams to visualize
        private static List<int> registeredVisualizationStreamTypes = new List<int>(0);         // the types of the registered streams to visualize
        private static double[] visualizationStreamValues = null;
        private static int visualizationStreamValueCounter = 0;

        // Visualization event(handler)s. An EventHandler delegate is associated with the event.
        // methods should be subscribed to this object
        public static event EventHandler<VisualizationValuesArgs> newVisualizationSourceInputValues = delegate { };
        public static event EventHandler<VisualizationValuesArgs> newVisualizationStreamValues = delegate { };
        public static event EventHandler<VisualizationEventArgs> newVisualizationEvent = delegate { };

        public static void Construct() {

            parameters.addParameter<bool>(
                "AllowDataVisualization",
                "Enable/disable the visualization of data.\nNote: Only turn this is off for performance benefits",
                "1");

            parameters.addParameter<string>(
                "DataDirectory",
                "Path to the directory to store the data in.\nThe path can be absolute (e.g. 'c:\\data\\') or relative (to the program executable, e.g. 'data\\').",
                "", "", "data\\");

            parameters.addParameter<string>(
                "Identifier",
                "A textual identifier.\nThe ID will be incorporated into the filename of the data files.",
                "", "", "");

            parameters.addParameter <bool>      (
                "LogSourceInput",
                "Enable/disable source input logging.\n\nNote: when there is no source input (for example when the source is a signal generator) then no source input data file will be created nor will there be any logging of source input",
                "1");

            parameters.addParameter<int>(
                "SourceInputMaxFilesize",
                "The maximum filesize for a source input data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
                "0");

            parameters.addParameter <bool>      (
                "LogDataStreams",
                "Enable/disable data stream logging.\nThis option will enable or disable the logging of data streams for all modules.\nEnabling or disabling specific data stream has to be done from the filter settings.\n\nNote: whether the streams that are being logged have values or zeros is dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain streams to be (values) or not be (zeros) logged.",
                "1");

            parameters.addParameter<int>(
                "SampleStreamMaxFilesize",
                "The maximum filesize for a stream data file.\nIf the data file exceeds this maximum, the data logging will continue in a sequentally numbered file with the same name.\n(set to 0 for no maximum)",
                "0");

            parameters.addParameter<bool>(
                "LogEvents",
                "Enable/disable event logging.",
                "1");

            parameters.addParameter<int[]>(
                "EventLoggingLevels",
                "Indicate which levels of event logging are allowed.\n(leave empty to log all levels)\n\nNote: whether events are logged or not is also dependent on the runtime configuration of the modules. It is possible\nthat the user, though an application module user-interface, sets certain events to be or not be logged.",
                "0");

        }

        /**
         * Configure the data class. Checks the values and application logic of the
         * parameters and, if valid, transfers the configuration parameters to local variables
         *
         * This will be called before all other configuration, which is why the registered stream are cleared here
         **/
        public static bool Configure() {

            // clear the registered source input streams
            registeredSourceInputStreamNames.Clear();
            registeredSourceInputStreamTypes.Clear();
            numSourceInputStreams = 0;

            // clear the registered data streams
            registeredDataStreamNames.Clear();
            registeredDataStreamTypes.Clear();
            numDataStreams = 0;

            // clear the registered visualization streams
            registeredVisualizationStreamNames.Clear();
            registeredVisualizationStreamTypes.Clear();
            numVisualizationStreams = 0;

            // check and transfer visualization parameter settings
            mAllowDataVisualization = parameters.getValue<bool>("AllowDataVisualization");
            Globals.setValue<bool>("AllowDataVisualization", mAllowDataVisualization ? "1" : "0");

            // check and transfer file parameter settings
            mLogSourceInput = parameters.getValue<bool>("LogSourceInput");
            mLogSourceInputRuntime = mLogSourceInput;
            mLogDataStreams = parameters.getValue<bool>("LogDataStreams");
            mLogDataStreamsRuntime = mLogDataStreams;
            mLogEvents = parameters.getValue<bool>("LogEvents");
            mLogEventsRuntime = mLogEvents;
            // ...

            // if there is data to store, create data directory
            if (mLogSourceInput || mLogDataStreams || mLogEvents) {

                // construct path name of data directory
                String dataDir = parameters.getValue<String>("DataDirectory");
                String path = Path.Combine(Directory.GetCurrentDirectory(), dataDir);

                // create the data directory 
                try {
                    if (Directory.Exists(path)) { logger.Info("Data directory already exists."); }    
                    else {
                        Directory.CreateDirectory(path);
                        logger.Info("Created data directory at " + path );
	                 }    
                } catch (Exception e) {
                    logger.Error("Unable to create data directory at " + path + " (" + e.ToString() + ")");
                } 

            }
            return true;

        }

        /**
         * Register a source input stream
         * Every source that wants to log a stream should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources that will log their samples)
         **/
        public static void RegisterSourceInputStream(string streamName, SampleFormat streamType) {
            
            // register a new stream
            registeredSourceInputStreamNames.Add(streamName);
            registeredSourceInputStreamTypes.Add(0);
            
            // add one to the total number of streams
            numSourceInputStreams++;

            // message
            logger.Debug("Registered source input stream '" + streamName + "' of the type ...");

        }

        public static int GetNumberOfSourceInputStreams() {
            return numSourceInputStreams;
        }

        /**
         * Register a data stream
         * Every module that wants to log a stream should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void RegisterDataStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredDataStreamNames.Add(streamName);
            registeredDataStreamTypes.Add(0);

            // add one to the total number of streams
            numDataStreams++;

            // message
            logger.Debug("Registered data stream '" + streamName + "' of the type ...");

        }

        /**
         * Register the visualization stream
         * Every module that wants to visualize a stream of samples should announce every stream respectively beforehand using this function
         * 
         * (this should be called during configuration, by all sources/filters/application that will log their samples)
         **/
        public static void RegisterVisualizationStream(string streamName, SampleFormat streamType) {

            // register a new stream
            registeredVisualizationStreamNames.Add(streamName);
            registeredVisualizationStreamTypes.Add(0);

            // add one to the total number of visualization streams
            numVisualizationStreams++;

            // message
            logger.Debug("Registered visualization stream '" + streamName + "' of the type ...");

        }

        public static int GetNumberOfVisualizationStreams() {
            return numVisualizationStreams;
        }

        public static string[] GetVisualizationStreamNames() {
            return registeredVisualizationStreamNames.ToArray<string>();
        }

        public static void Start() {

            // get location of data directory and current time to use as timestamp for files to be created
            String dataDir = parameters.getValue<String>("DataDirectory");
            String fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            // TODO make subdirs (+ add param to set and check this)
            // TODO create (and close) the parameter file
            // TODO create the event file

            // check if we want to log the source input
            if (mLogSourceInput && numSourceInputStreams > 0) {

                // (re)start the stopwatch here, so the elapsed timestamp of the first sample will be the time from start till the first sample coming in
                sourceStopWatch.Restart();

                // log the source start event
                LogEvent(1, "SourceStart", "");

                // (re)set sample counter
                sourceSampleCounter = 0;

                // construct filepath of source file, with current time as filename 
                String fileNameSrc = fileName + ".src";
                String path = Path.Combine(Directory.GetCurrentDirectory(), dataDir, fileNameSrc);

                // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                try {
                    sourceStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                    sourceStreamWriter = new BinaryWriter(sourceStream);
                    logger.Info("Created source file at " + path);
                } catch (Exception e) {
                    logger.Error("Unable to create source file at " + path + " (" + e.ToString() + ")");
                }

                // write header
                writeHeader(registeredSourceInputStreamNames, sourceStreamWriter);
            }

            // check if there are any samples streams to be logged
            if (mLogDataStreams && numDataStreams > 0) {

                // (re)start the stopwatch here, so the elapsed timestamp of the first sample will be the time from start till the first sample coming in
                dataStopWatch.Restart();

                // log the data start event
                LogEvent(1, "DataStart", "");

                // (re)set sample counter
                dataSampleCounter = 0;

                // resize dataStreamValues array in order to hold all values from all data streams
                dataStreamValues = new double[numDataStreams];

                // construct filepath of data file, with current time as filename 
                String fileNameDat = fileName + ".dat";
                String path = Path.Combine(Directory.GetCurrentDirectory(), dataDir, fileNameDat);

                // create filestream: create file if it does not exists, allow to write, do not share with other processes and use buffer of 8192 bytes (roughly 1000 samples)
                try {
                    dataStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192);
                    dataStreamWriter = new BinaryWriter(dataStream);
                    logger.Info("Created data file at " + path);
                } catch (Exception e) {
                    logger.Error("Unable to create data file at " + path + " (" + e.ToString() + ")");
                }

                // write header
                writeHeader(registeredDataStreamNames, dataStreamWriter);
            }


            // check if data visualization is enabled
            if (mAllowDataVisualization) {

                // size the array to fit all the streams
                visualizationStreamValues = new double[numVisualizationStreams];

            }

        }

        public static void Stop() {
            
            // log the source and data stop event
            LogEvent(1, "DataStop", "");
            LogEvent(1, "SourceStop", "");

            // close the source and data stream file
            dataStreamWriter.Close();
            sourceStreamWriter.Close();

            // close the event file

            // if there is still a stopwatch running, stop it
            if (dataStopWatch.IsRunning) { dataStopWatch.Stop(); }
            if (sourceStopWatch.IsRunning) { sourceStopWatch.Stop(); }
        }

        public static void Destroy() {

            // stop the Data Class

			// TODO: Finalize stopwatches
        }

        /**
         * Called when a sample is at the beginning of the pipeline (from before the first filter module till after the application module)
         **/
        public static void SampleProcessingStart() {

            // reset the array pointers for data samples
            dataValuePointer = 0;

            // store the milliseconds past since the last sample and reset the data stopwatch timer
            dataElapsedTime = dataStopWatch.ElapsedMilliseconds;
            dataStopWatch.Restart();

            // reset the visualization data stream counter
            visualizationStreamValueCounter = 0;
        }

        /**
         * Called when a sample is at the end of the pipeline (from before the first filter module till after the application module)
         **/
        public static void SampleProcessingEnd() {

            // debug, show data values being stored
            logger.Info("To .dat file: " + dataElapsedTime + " " + dataSampleCounter + " " + String.Join(",", dataStreamValues.Select(p => p.ToString()).ToArray()));
            
            // TODO? create function that stores data that can be used for both src and dat?

            // integrity check of collected data stream values: if the pointer is not exactly at end of array, not all values have been
            // delivered or stored, else transform to bytes and write to file
            if (dataValuePointer != numDataStreams) { 
				logger.Error("Not all data values have been stored in the .dat file");
			} else {

                    // transform variables that will be stored in .dat to binary arrays (except for dataStreamValues array which is copied directly)
                    byte[] dataElapsedTimeBinary = BitConverter.GetBytes(dataElapsedTime);
                    byte[] dataSampleCounterBinary = BitConverter.GetBytes(dataSampleCounter);

                    // create new array to hold all bytes
                    int l1 = dataElapsedTimeBinary.Length;
                    int l2 = dataSampleCounterBinary.Length;
                    int l3 = dataStreamValues.Length * sizeof(double);
                    byte[] streamOut = new byte[l1 + l2 + l3];

                    // blockcopy all bytes to this array
                    Buffer.BlockCopy(dataElapsedTimeBinary, 0, streamOut, 0, l1);
                    Buffer.BlockCopy(dataSampleCounterBinary, 0, streamOut, l1, l2);
                    Buffer.BlockCopy(dataStreamValues, 0, streamOut, l1 + l2, l3);
                    
                    // write data to file
                    dataStreamWriter.Write(streamOut);     
            }

            // advance sample counter, if gets to max value, reset to 0
            if (++dataSampleCounter == uint.MaxValue) dataSampleCounter = 0;

            // check if data visualization is allowed
            if (mAllowDataVisualization) {

                // check if the number of values that came in is the same as the number of streams we expected to come in (registered)
                if (visualizationStreamValueCounter == numVisualizationStreams) {
                    // number of values matches
                    
                    // trigger a new data stream values event for visualization
                    VisualizationValuesArgs args = new VisualizationValuesArgs();
                    args.values = visualizationStreamValues;
                    newVisualizationStreamValues(null, args);

                } else {
                    // number of values mismatches

                    // message
                    logger.Error("Not the same number of visualization streams (" + visualizationStreamValueCounter + ") are logged than have been registered (" + numVisualizationStreams + ") for logging, discarded, check code");

                }

            }

        }

        /**
         * Log raw source input values to the source input file (.src) 
         * 
         **/
        public static void LogSourceInputValues(double[] sourceStreamValues) {
            
            // get time since last source sample
            sourceElapsedTime = sourceStopWatch.ElapsedMilliseconds;
            sourceStopWatch.Restart();

            // integrity check of collected source values: if the length of the incoming array is not equal to number of registered source streams, not all values
            // delivered or stored, else transform to bytes and write to file
            if (sourceStreamValues.Length != numSourceInputStreams) {
                logger.Error("Not all source values have been stored in the .src file");
            }
            else {

                // transform variables that will be stored in .src to binary arrays (except for sourceStreamValues array which is copied directly)
                byte[] sourceElapsedTimeBinary = BitConverter.GetBytes(sourceElapsedTime);
                byte[] sourceSampleCounterBinary = BitConverter.GetBytes(sourceSampleCounter);

                // create new array to hold all bytes
                int l1 = sourceElapsedTimeBinary.Length;
                int l2 = sourceSampleCounterBinary.Length;
                int l3 = sourceStreamValues.Length * sizeof(double);
                byte[] streamOut = new byte[l1 + l2 + l3];

                // blockcopy all bytes to this array
                Buffer.BlockCopy(sourceElapsedTimeBinary, 0, streamOut, 0, l1);
                Buffer.BlockCopy(sourceSampleCounterBinary, 0, streamOut, l1, l2);
                Buffer.BlockCopy(sourceStreamValues, 0, streamOut, l1 + l2, l3);

                // write source to file
                sourceStreamWriter.Write(streamOut);
            }

            // advance sample counter, if gets to max value, reset to 0
            if (++sourceSampleCounter == uint.MaxValue) sourceSampleCounter = 0;

            // debug
            logger.Info("To .src file: " + sourceElapsedTime + " " + sourceSampleCounter + " " + String.Join(",", sourceStreamValues.Select(p => p.ToString()).ToArray()));

            // check if data visualization is allowed
            if (mAllowDataVisualization) {

                // trigger a new source input values event for visualization
                VisualizationValuesArgs args = new VisualizationValuesArgs();
                args.values = sourceStreamValues;
                newVisualizationSourceInputValues(null, args);

            }

        }
        public static void LogSourceInputValues(ushort[] values) {
            
            // TODO: temp until we have a standard format, we might want to store as ushort
            // but for now convert ushorts to double and call the double[] overload
            double[] dblValues = new double[values.Length];
            for (int i = 0; i < values.Length; i++) {
                dblValues[i] = values[i];
            }
            LogSourceInputValues(dblValues);

        }

        /**
         * Log a raw stream value to the stream file (.dat) 
         * 
         **/
        public static void LogStreamValue(double value) {
            //Console.WriteLine("LogStreamValue");

            // check if the counter is within the size of the value array
            if (dataValuePointer >= numDataStreams) {
                
                // message
                logger.Error("More data streams are logged than have been registered for logging, discarded, check code (currently logging at values array index " + dataValuePointer + ")");

            } else {

                // store the incoming value in the array, advance the pointer
                dataStreamValues[dataValuePointer] = value;
                dataValuePointer++;

            }

        }

        /**
         * Log events to the events file (.evt) 
         * 
         **/
        public static void LogEvent(int level, string text, string value) {

        }

        /**
         * Log a raw stream value to visualize
         * 
         **/
        public static void LogVisualizationStreamValue(double value) {
            if (!mAllowDataVisualization)   return;

            // check if the counter is within the size of the value array
            if (visualizationStreamValueCounter >= numVisualizationStreams) {
                // not within the size of the values array, more visualization streams are logged than have been registered for logging

                // message
                logger.Error("More visualization streams are logged than have been registered for logging, discarded, check code (currently logging at values array index " + visualizationStreamValueCounter + ")");

            } else {
                // within the size of the values array

                // set the value
                visualizationStreamValues[visualizationStreamValueCounter] = value;
                visualizationStreamValueCounter++;

            }

        }

        /**
         * Log raw source input data to visualize
         * 
         **/
        public static void LogVisualizationEvent(int level, string text, string value) {

            VisualizationEventArgs args = new VisualizationEventArgs();
            args.level = level;
            args.text = text;
            args.value = value;
            newVisualizationEvent(null, args);

        }

        /**
         * Create log file of given type
         * 
         **/
        public static void writeHeader(List<string> streamNames, BinaryWriter writer) {

            // create header: convert list with names of streams to String, with tabs between names 
            String header = string.Join("\t", streamNames.ToArray());
            header = "Elapsed time [ms] \t Sample # \t" + header;
            byte[] headerBinary = Encoding.ASCII.GetBytes(header);

            // store length [bytes] of header 
            int headerLen = headerBinary.Length;
            byte[] headerLenBinary = BitConverter.GetBytes(headerLen);

            // creat output byte array and copy length of header and header into this array
            byte[] headerOut = new byte[sizeof(int) + headerLen];
            Buffer.BlockCopy(headerLenBinary, 0, headerOut, 0, sizeof(int));
            Buffer.BlockCopy(headerBinary, 0, headerOut, sizeof(int), headerLen);

            // write header to file
            writer.Write(headerOut);   
        }

    }

}
