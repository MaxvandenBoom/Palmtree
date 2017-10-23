﻿using NLog;
using System;
using System.IO;

using UNP.Core;
using UNP.Applications;
using UNP.Core.Helpers;
using UNP.Core.Params;
using UNP.Core.DataIO;


namespace LocalizerTask {

    public class LocalizerTask : IApplication {

        // fundamentals
        private const int CLASS_VERSION = 1;                                // class version
        private const string CLASS_NAME = "LocalizerTask";                  // class name
        private const string CONNECTION_LOST_SOUND = "sounds\\focuson.wav";

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);    // the logger object for the view
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);        // parameters
 
        // status 
        private bool unpMenuTask = false;                                   // flag whether the task is created by the UNPMenu
        private bool unpMenuTaskRunning = false;                            // flag to hold whether the task is running as UNP task
        private bool unpMenuTaskSuspended = false;                         // flag to hold whether the task (when running as UNP task) is suspended          

        private bool mConnectionLost = false;							// flag to hold whether the connection is lost
        private bool mConnectionWasLost = false;						// flag to hold whether the connection has been lost (should be reset after being re-connected)
        private System.Timers.Timer mConnectionLostSoundTimer = null;       // timer to play the connection lost sound on

        private TaskStates taskState = TaskStates.Start;                    // holds current task state
        private TaskStates previousTaskState = TaskStates.Start;            // holds previous task state
        private int waitCounter = 0;                                        // counter for task state Start and Wait, used to determine time left in this state

        // view
        private LocalizerView view = null;                           // view for task
        private Object lockView = new Object();                             // threadsafety lock for all event on the view
        private int windowLeft = 0;                                         // position of window from left of screen
        private int windowTop = 0;                                          // position of windo from top of screen
        private int windowWidth = 800;                                      // window width
        private int windowHeight = 600;                                     // window height
        private int windowRedrawFreqMax = 0;                                // redraw frequency
        private RGBColorFloat windowBackgroundColor = new RGBColorFloat(0f, 0f, 0f);    // background color of view

        // task specific
        private string[][] stimuli = null;                                  // matrix with stimulus information per stimulus: text, sound, image, length
        private int[] stimuliSequence = null;                               // sequence of stimuli, referring to stimuli by index in stimuli matrix 
        private int currentStimulus = 0;                                    // stimulus currently being presented, referred to by index in stimuli matrix
        private int stimulusCounter = 0;                                    // stimulus pointer, indicating which stimulus of sequence is currently being presented
        private int stimulusRemainingTime = -1;                             // remaining time to present current stimulus, in samples
        private int amountSequences = 0;                                    // amount of stimuli sequences that will be presented
        private int currentSequence = 1;                                    // the current sequence being presented
        private int firstSequenceWait = 0;                                  // time to wait at start of task before first stimulus is presented, in samples
        private int sequenceWait = 0;                                       // time to wait between end of sequence and start of next sequence, in samples
        private string startText = "";                                      // text presented to participant at beginning of task
        private string waitText = "";                                       // text presented to participant during waiting
        private string endText = "";                                        // text presented to participant at end of task
        private enum TaskStates : int {                                     // taskstates
            Start,
            Run,
            Wait,
            Pause,
            End
        };

        // parameterless constructor calls second constructor
        public LocalizerTask() : this(false) { }
        public LocalizerTask (bool UNPMenuTask) {

            // transfer the UNP menu task flag
            this.unpMenuTask = UNPMenuTask;

            // check if the task is standalone (not unp menu)
            if (!this.unpMenuTask) {

                // create a parameter set for the task
                parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Application);

                // add parameters
                parameters.addParameter<int>(
                    "WindowLeft",
                    "Screen coordinate of application window's left edge",
                    "", "", "0");

                parameters.addParameter<int>(
                    "WindowTop",
                    "Screen coordinate of application window's top edge",
                    "", "", "0");

                parameters.addParameter<int>(
                    "WindowWidth",
                    "Width of application window (fullscreen and 0 will take monitor resolution)",
                    "", "", "800");

                parameters.addParameter<int>(
                    "WindowHeight",
                    "Height of application window (fullscreen and 0 will take monitor resolution)",
                    "", "", "600");

                parameters.addParameter<int>(
                    "WindowRedrawFreqMax",
                    "Maximum display redraw interval in FPS (0 for as fast as possible)",
                    "0", "", "50");

                parameters.addParameter<RGBColorFloat>(
                    "WindowBackgroundColor",
                    "Window background color",
                    "", "", "0");

                parameters.addParameter<string[][]>(
                    "Stimuli",
                    "Stimuli available for use in sequence and their corresponding duration in seconds. Stimuli can be either text, images, or sounds, or any combination of these modalities.\n Each row represents a stimulus.",
                    "", "", "Rust;;;10", new string[] { "Text", "Image", "Sound", "Duration [s]" });

                parameters.addParameter<int[]>(
                    "StimuliSequence",
                    "",
                    "Sequence of presented stimuli.", "", "", "1");

                parameters.addParameter<int>(
                    "AmountOfSequences",
                    "Number of sequences that will be presented consecutively.",
                    "1", "", "2");

                parameters.addParameter<int>(
                    "FirstSequenceWait",
                    "Amount of time before the first sequence of the task starts.",
                    "0", "", "5s");

                parameters.addParameter<int>(
                    "SequenceWait",
                    "Amount of time between end of sequence and start of subsequent sequence.",
                    "0", "", "10s");

                parameters.addParameter<string>(
                    "StartText",
                    "Text shown to participant at beginning of task.",
                    "", "", "Task will begin shortly.");

                parameters.addParameter<string>(
                    "WaitText",
                    "Text shown to participant during waiting periods.",
                    "", "", "Wait.");

                parameters.addParameter<string>(
                    "EndText",
                    "Text shown to participant at end of task.",
                    "", "", "Task is finished.");

            }
        }

        public Parameters getParameters() {
            return parameters;
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public string getClassName() {
            return CLASS_NAME;
        }

        public bool configure(ref PackageFormat input) {

            // PARAMETER TRANSFER 

            // transfer window settings
            windowLeft = parameters.getValue<int>("WindowLeft");
            windowTop = parameters.getValue<int>("WindowTop");
            windowWidth = parameters.getValue<int>("WindowWidth");
            windowHeight = parameters.getValue<int>("WindowHeight");
            windowRedrawFreqMax = parameters.getValue<int>("WindowRedrawFreqMax");
            windowBackgroundColor = parameters.getValue<RGBColorFloat>("WindowBackgroundColor");

            // transfer task specific values
            stimuli = parameters.getValue<string[][]>("Stimuli");
            stimuliSequence = parameters.getValue<int[]>("StimuliSequence");
            amountSequences = parameters.getValue<int>("AmountOfSequences");
            firstSequenceWait = parameters.getValueInSamples("FirstSequenceWait");
            sequenceWait = parameters.getValueInSamples("SequenceWait");
            startText = parameters.getValue<string>("StartText");
            waitText = parameters.getValue<string>("WaitText");
            endText = parameters.getValue<string>("EndText");


            // PARAMETER CHECK
            // TODO: parameters.checkminimum, checkmaximum

            // check if stimuli are defined
            if (stimuli.Length != 4) {
                logger.Error("Stimuli not defined (correctly).");
                return false;
            } else {
                // if stimuli are defined, check if duration is defined for each stimulus
                for (int i = 0; i < stimuli[3].Length; i++) {
                    if (string.IsNullOrEmpty(stimuli[3][i]) || stimuli[3][i] == " ") {
                        logger.Error("Timing of stimulus " + (i+1).ToString() + " is not defined.");
                        return false;
                    } else {
                        // if timing is defined, see if it is parsable to integer
                        int timing = 0;
                        if(!int.TryParse(stimuli[3][i], out timing)) {
                            logger.Error("The timing given for stimulus " + (i+1).ToString() + " (\"" + stimuli[3][i] + "\") is not a valid value, should be a positive integer.");
                            return false;
                        }
                        // if timing is parsable, check if larger than 0
                        else if (timing <= 0){
                            logger.Error("The timing given for stimulus " + (i+1).ToString() + " (" + stimuli[3][i] + ") is too short. The value should be a positive integer.");
                            return false;
                        }
                    }
                }
            }

            // check if stimulus sequence is defined
            if(stimuliSequence.Length <= 0) {
                logger.Error("No stimulus sequence given.");
                return false;
            }

            // determine maximal stimulus defined in stimulus sequence
            int stimMax = 0;
            for(int i=0; i<stimuliSequence.Length; i++) {
                if (stimuliSequence[i] > stimMax) stimMax = stimuliSequence[i];
            }

            // check if there are stimuli defined that are not included in stimuli definition
            if(stimMax > stimuli[0].Length) {
                logger.Error("In stimulus sequence, stimulus " + stimMax + " is defined. This stimulus can not be found in stimuli definition, as there are only " + stimuli[0].Length + " stimuli defined.");
                return false;
            }

            // check if amount of sequences is higher than 0
            if (amountSequences <= 0) {
                logger.Error("Amount of sequences should be a positive integer.");
                return false;
            }

            // check if first sequence wait is not smaller than 0
            if (firstSequenceWait < 0) {
                logger.Error("The time to wait before the start of the first sequence can not be lower than 0.");
                return false;
            }

            // check if sequence wait is not smaller than 0
            if (sequenceWait < 0) {
                logger.Error("The time to wait before the start of the subsequent sequence can not be lower than 0.");
                return false;
            }

            // view checks
            if (windowRedrawFreqMax < 0) {
                logger.Error("The maximum window redraw frequency can be no smaller then 0");
                return false;
            }

            if (windowWidth < 1) {
                logger.Error("The window width can be no smaller then 1");
                return false;
            }

            if (windowHeight < 1) {
                logger.Error("The window height can be no smaller then 1");
                return false;
            }

            return true;
        }

        public void initialize() {
                    
            // lock for thread safety
            lock (lockView) {

                // check the view (thread) already exists, stop and clear the old one.
                destroyView();

                // initialize view (scene)
                initializeView();

            }

        }

        private void initializeView() {

            // create scene thread
            view = new LocalizerView(windowRedrawFreqMax, windowLeft, windowTop, windowWidth, windowHeight, false);
            view.setBackgroundColor(windowBackgroundColor.getRed(), windowBackgroundColor.getGreen(), windowBackgroundColor.getBlue());

            // start the scene thread
            view.start();
        }

        public void start() {

            // lock for thread safety
            lock (lockView) {

                // check if view is available
                if (view == null) return;

                // log event task is started
                Data.logEvent(2, "TaskStart", CLASS_NAME);

                // feedback to user
                logger.Info("Task started.");

                // if a wait before first sequence is required, set state to Start, else to run 
                if (firstSequenceWait != 0)     setState(TaskStates.Start);
                else                            setState(TaskStates.Run);

            }

        }

        public void stop() {

            // set state to End if not already in this state
            if(taskState != TaskStates.End)     setState(TaskStates.End);
        }

        public bool isStarted() {
            return true;
        }

        public void process(double[] input) {

            // lock for thread safety
            lock (lockView) {

                if (view == null) return;

                ////////////////////////
                // BEGIN CONNECTION FILTER ACTIONS//
                ////////////////////////

                // check if connection is lost, or was lost
                if (mConnectionLost) {

                    // check if it was just discovered if the connection was lost
                    if (!mConnectionWasLost) {
                        // just discovered it was lost

                        // set the connection as was lost (this also will make sure the lines in this block willl only run once)
                        mConnectionWasLost = true;

                        // pauze the task
                        pauzeTask();

                        // show the lost connection warning
                        view.setConnectionLost(true);

                        // play the connection lost sound
                        Sound.Play(CONNECTION_LOST_SOUND);

                        // setup and start a timer to play the connection lost sound every 2 seconds
                        mConnectionLostSoundTimer = new System.Timers.Timer(2000);
                        mConnectionLostSoundTimer.Elapsed += delegate (object source, System.Timers.ElapsedEventArgs e) {

                            // play the connection lost sound
                            Sound.Play(CONNECTION_LOST_SOUND);

                        };
                        mConnectionLostSoundTimer.AutoReset = true;
                        mConnectionLostSoundTimer.Start();

                    }

                    // do not process any further
                    return;

                } else if (mConnectionWasLost && !mConnectionLost) {
                    // if the connection was lost and is not lost anymore

                    // stop and clear the connection lost timer
                    if (mConnectionLostSoundTimer != null) {
                        mConnectionLostSoundTimer.Stop();
                        mConnectionLostSoundTimer = null;
                    }

                    // hide the lost connection warning
                    view.setConnectionLost(false);

                    // resume task
                    resumeTask();

                    // reset connection lost variables
                    mConnectionWasLost = false;

                }

                ////////////////////////
                // END CONNECTION FILTER ACTIONS//
                ////////////////////////



                // perform actions for state
                switch (taskState) {

                    case TaskStates.Start:

                        // wait until timer reaches zero, then go to Run state
                        if (waitCounter == 0)   setState(TaskStates.Run);
                        else                    waitCounter--;

                        break;

                    case TaskStates.Run:

                        // if the presentation time for this stimulus is up
                        if (stimulusRemainingTime == 0) {

                            // increase stimulus pointer
                            stimulusCounter++;

                            // if we reached end of stimulus sequence, set stimulus pointer to 0, and move to next sequence
                            if (stimulusCounter >= stimuliSequence.Length) {
                                stimulusCounter = 0;
                                currentSequence++;

                                if (currentSequence > amountSequences)      setState(TaskStates.End);       // if we have presented all sequences, go to task state End
                                else if (sequenceWait != 0) {               waitCounter = sequenceWait;     // if there are sequences to present and there is a time to wait, set waiting time and go to state Wait
                                                                            setState(TaskStates.Wait); } 
                                else                                        setStimulus();                  // if there are sequences to present and we do not have to wait, set next stimulus

                            }
                            // if we are not at end of sequence, present next stimulus 
                            else                                            setStimulus();

                        // if time is not up, decrease remaining time
                        } else stimulusRemainingTime--;

                        break;

                    case TaskStates.Wait:

                        // if there is time left to wait, decrease time, otherwise return to Run state
                        if (waitCounter != 0)   waitCounter--;
                        else                    setState(TaskStates.Run);

                        break;
                        
                    case TaskStates.Pause:
                        break;

                    case TaskStates.End:
                        break;

                    default:
                        logger.Info("Non-existing task state reached. Task will be stopped. Check code.");
                        stop();
                        break;

                }

            }

        }

        // set state of task to given state
        private void setState(TaskStates state) {

            // check if there is a view to modify
            if (view == null) return;

            // store the previous state
            previousTaskState = taskState;

            // transfer state
            taskState = state;

            // perform initial actions for new state
            switch (taskState) {

                case TaskStates.Start:

                    // set text to display 
                    view.setText(startText);

                    // feedback to user
                    logger.Info("Participant is waiting for task to begin.");

                    // log event participant is waiting
                    Data.logEvent(2, "WaitPresented", "");

                    // initialize wait counter, determining how long this state will last
                    waitCounter = firstSequenceWait;

                    break;

                case TaskStates.Run:

                    // show stimulus
                    setStimulus();

                    break;

                case TaskStates.Wait:

                    // set text to display 
                    view.setText(waitText);

                    // feedback to user
                    logger.Info("Participant is waiting for next stimulus.");

                    // log event participant is waiting
                    Data.logEvent(2, "WaitPresented", "");

                    break;

                case TaskStates.Pause:

                    // set text to display 
                    view.setText("Task paused.");

                    // feedback to user
                    logger.Info("Task paused.");

                    // log event task is paused
                    Data.logEvent(2, "TaskPause", CLASS_NAME);

                    break;

                case TaskStates.End:

                    // set text to display 
                    view.setText(endText);

                    // feedback to user
                    logger.Info("Task finished.");

                    // log event that task is stopped
                    Data.logEvent(2, "TaskStop", CLASS_NAME);

                    // reset all relevant variables in case task is restarted
                    currentStimulus = stimulusCounter = 0;
                    currentSequence = 1;
                    stimulusRemainingTime = -1;

                    // stop sources, filters etc through Mainthread
                    MainThread.stop();

                    break;

                default:
                    logger.Info("Non-existing task state selected. Task will be stopped. Check code.");
                    stop();
                    break;
            }
        }

        private void pauzeTask() {
            if (view == null) return;

            // log event task is paused
            Data.logEvent(2, "TaskPause", CLASS_NAME);
            

        }
        private void resumeTask() {

            // log event task is resumed
            Data.logEvent(2, "TaskResume", CLASS_NAME);

            // set the previous state
            setState(previousTaskState);
        }

        public static void playSound(string filename) {

            // check if the file exists
            if (!File.Exists(filename)) {
                logger.Error("Could not play soundfile '" + filename + "'");
                return;
            }

            // play the file
            try {
                new System.Media.SoundPlayer(filename).Play();
            } catch (Exception) {
                logger.Error("Could not play soundfile '" + filename + "'");
                return;
            }
        }

        private void setStimulus() {

            // determine current stimulus based on stimulus sequence (-1 because 0-based)
            currentStimulus = stimuliSequence[stimulusCounter] - 1;

            // feedback to user
            logger.Info("Presenting stimulus " + (currentStimulus+1) + " (" + stimuli[0][currentStimulus] + ") in sequence " + currentSequence + " for " + stimuli[3][currentStimulus] + " seconds");

            // log event that stimulus is presented
            Data.logEvent(2, "StimulusPresented", currentStimulus.ToString());

            // TODO: set stimulus sound and image
            // set stimulus text to display 
            view.setText(stimuli[0][currentStimulus]);

            // if the stimulus remaining time is not set or is zero, set the remaining time for this stimulus 
            if (stimulusRemainingTime <= 0) {

                // try to parse stimulus presentation time
                if (int.TryParse(stimuli[3][currentStimulus], out stimulusRemainingTime)) {

                    // determine remaining stimulus time in samples
                    // TODO: this should be in ParamStringMat.getvalueInSamples()
                    double samples = stimulusRemainingTime * MainThread.SamplesPerSecond();
                    stimulusRemainingTime = (int)Math.Round(samples);

                    // give warning if rounding occurs
                    if (samples != stimulusRemainingTime) logger.Warn("Remaining time for presenting current stimulus (stimulus " + currentStimulus + ") has been rounded from " + samples + " samples to " + stimulusRemainingTime + " samples.");

                } else {

                    // if parsing of presentation time of stimulus fails, stop task
                    logger.Error("The timing given for stimulus " + (currentStimulus + 1).ToString() + " (\"" + stimuli[3][currentStimulus] + "\") is not a valid value, should be a positive integer. Execution of task stops.");
                    stop();
                }
            }
        }

        public void destroy() {

            // stop the application
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
            stop();

            // lock for thread safety
            lock (lockView) {

                // destroy the view
                destroyView();

                // stop and clear the connection lost timer
                if (mConnectionLostSoundTimer != null) {
                    mConnectionLostSoundTimer.Stop();
                    mConnectionLostSoundTimer = null;
                }

            }

            // destroy/empty more task variables

        }

        private void destroyView() {
            
            // check if a scene thread still exists
            if (view != null) {

                // stop the animation thread (stop waits until the thread is finished)
                view.stop();

                // release the thread (For collection)
                view = null;

            }

        }

        //
        //  UNP entry points (start, process, stop)
        //
        public void UNP_start(Parameters parentParameters) {

            // UNP entry point can only be used if initialized as UNPMenu
            if (!this.unpMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor.");
                return;
            }

            // set window settings
            windowRedrawFreqMax = parentParameters.getValue<int>("WindowRedrawFreqMax");      // the view update frequency (in maximum fps)
            windowWidth = parentParameters.getValue<int>("WindowWidth"); ;
            windowHeight = parentParameters.getValue<int>("WindowHeight"); ;
            windowLeft = parentParameters.getValue<int>("WindowLeft"); ;
            windowTop = parentParameters.getValue<int>("WindowTop"); ;

            // set task specific variables
            stimuli = new string[][] { new string[] {"Rust","Concentreer"}, new string[] { "","" }, new string[] { "", "" }, new string[] { "10", "5" } };
            stimuliSequence = new int[] { 1, 2, 1};
            amountSequences = 2;
            firstSequenceWait = 10 * (int)MainThread.SamplesPerSecond();
            sequenceWait = 5 * (int)MainThread.SamplesPerSecond();
            startText = "Task will begin shortly";
            waitText = "Wait";
            endText = "End task";

            // initialize
            initialize();

            // start the task
            start();

            // set the task as running as UNP task
            unpMenuTaskRunning = true;
        }

        public void UNP_stop() {

            // UNP entry point can only be used if initialized as UNPMenu
            if (!this.unpMenuTask) {
                logger.Error("Using UNP entry point while the task was not initialized as UNPMenu task, check parameters used to call the task constructor");
                return;
            }

            // stop the task from running
            stop();

            // destroy the task
            destroy();

            // flag the task as no longer running (setting this to false is also used to notify the UNPMenu that the task is finished)
            unpMenuTaskRunning = false;
        }

        public bool UNP_isRunning() {
            return unpMenuTaskRunning;
        }

        public void UNP_process(double[] input, bool unpConnectionLost) {

            // check if the task is running
            if (unpMenuTaskRunning) {

                // transfer connection lost
                mConnectionLost = unpConnectionLost;

                // process the input (if the task is not suspended)
                if (!unpMenuTaskSuspended) process(input);

            }

        }

        public void UNP_resume() {

            // flag task as no longer suspended
            unpMenuTaskSuspended = false;
            
            // resume the task
            resumeTask();
        }

        public void UNP_suspend() {

            // flag task as suspended
            unpMenuTaskSuspended = true;

            // pause the task
            setState(TaskStates.Pause);
        }

    }

}