using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    public enum LaunchStepState { Pending, Loading, Done, Failed }

    // One preparation step on the launch screen: a named group of bundled files that must (or may) load.
    public sealed class LaunchStep
    {
        public string Name { get; private set; }
        public bool Required { get; private set; }
        public int Total { get; private set; }
        public int Loaded { get; internal set; }
        public LaunchStepState State { get; internal set; }
        public string Error { get; internal set; }
        internal LaunchStep(string name,bool required,int total) { Name = name; Required = required; Total = Math.Max(1,total); }
    }

    // The launch screen's truth: progress is files actually loaded, a missing required file blocks entry with a
    // retry, a missing optional one (sound, music) only warns. Pure state; the runtime loader reports into it.
    public sealed class LaunchSequence
    {
        private readonly List<LaunchStep> steps = new List<LaunchStep>();
        public IList<LaunchStep> Steps { get { return steps.AsReadOnly(); } }
        public int Attempts { get; private set; }
        public LaunchSequence Add(string name,bool required,int total) { steps.Add(new LaunchStep(name,required,total)); return this; }
        public LaunchStep Find(string name) { return steps.Find(s => s.Name == name); }
        public float Progress
        {
            get {
                int total = 0, loaded = 0;
                foreach (var s in steps) { total += s.Total; loaded += s.State == LaunchStepState.Done ? s.Total : Math.Min(s.Loaded,s.Total); }
                return total == 0 ? 1 : (float)loaded / total;
            }
        }
        // The step being worked on, or the first that still waits.
        public LaunchStep Current { get { return steps.Find(s => s.State == LaunchStepState.Loading) ?? steps.Find(s => s.State == LaunchStepState.Pending); } }
        public bool Finished { get { return steps.TrueForAll(s => s.State == LaunchStepState.Done || s.State == LaunchStepState.Failed); } }
        public bool Blocked { get { return steps.Exists(s => s.Required && s.State == LaunchStepState.Failed); } }
        public bool HasWarnings { get { return steps.Exists(s => !s.Required && s.State == LaunchStepState.Failed); } }
        public bool CanEnter { get { return Finished && !Blocked; } }

        public void Begin(LaunchStep step) { if (step.State == LaunchStepState.Pending) { step.State = LaunchStepState.Loading; step.Loaded = 0; step.Error = null; } }
        public void Report(LaunchStep step,int loaded) { if (step.State == LaunchStepState.Loading) step.Loaded = Math.Max(0,Math.Min(step.Total,loaded)); }
        public void Complete(LaunchStep step) { if (step.State == LaunchStepState.Loading) { step.State = LaunchStepState.Done; step.Loaded = step.Total; } }
        public void Fail(LaunchStep step,string error) { if (step.State == LaunchStepState.Loading) { step.State = LaunchStepState.Failed; step.Error = string.IsNullOrEmpty(error) ? "Could not load." : error; } }
        // Failed steps go back to waiting; finished ones are kept.
        public void Retry()
        {
            Attempts++;
            foreach (var s in steps) if (s.State == LaunchStepState.Failed) { s.State = LaunchStepState.Pending; s.Loaded = 0; s.Error = null; }
        }
    }
}
