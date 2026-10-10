using System;
using System.Threading;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using UnityEngine.Profiling;

namespace SwapHunter
{
    // Same synchronous GC.Alloc scope and main-thread filter used by Unity's performance test package.
    // Sample.Count is allocation events. Sample.Value is bytes ONLY if the reported unit and probe verify it.
    public sealed class SynchronousGcAllocationRecorder : IDisposable
    {
        [Serializable] public sealed class Verification
        {
            public string method = "GC.Alloc ProfilerRecorder / CollectOnlyOnCurrentThread / synchronous Start-Stop-GetSample";
            public string unit = "unavailable", failure;
            public bool recorderValid, profilerEnabled, readImmediatelyAfterStop, eventsVerified, bytesVerified;
            public int threadId, positiveProbePayloadBytes = 4096, positiveProbeSamples, emptyProbeSamples;
            public long emptyProbeRawValue = -1, emptyProbeEvents = -1;
            public long positiveProbeRawValue = -1, positiveProbeEvents = -1;
        }
        public struct Sample
        {
            public bool hadSample;
            public long managedBytes, allocationEvents;
        }
        ProfilerRecorder recorder;
        bool created, running;
        readonly int threadId;
        public Verification Evidence { get; } = new Verification();
        public SynchronousGcAllocationRecorder()
        {
            threadId = Thread.CurrentThread.ManagedThreadId; Evidence.threadId = threadId;
            Evidence.profilerEnabled = Profiler.enabled;
            try
            {
                recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                created = true; Evidence.recorderValid = recorder.Valid;
                if (!recorder.Valid) { Evidence.failure = "GC.Alloc recorder unavailable in this build; a Development player may expose the marker."; return; }
                Evidence.unit = recorder.UnitType.ToString();
                recorder.Reset(); recorder.Start(); recorder.Stop();
                Evidence.emptyProbeSamples = recorder.Count;
                if (recorder.Count > 0)
                { var empty = recorder.GetSample(0); Evidence.emptyProbeRawValue = empty.Value; Evidence.emptyProbeEvents = empty.Count; }
                recorder.Reset(); recorder.Start();
                var positive = new byte[4096]; positive[0] = 1;
                recorder.Stop(); GC.KeepAlive(positive);
                Evidence.readImmediatelyAfterStop = true; Evidence.positiveProbeSamples = recorder.Count;
                if (recorder.Count == 0) { Evidence.failure = "No synchronous sample after the known 4096-byte allocation; no zero-allocation inference is allowed."; return; }
                var sample = recorder.GetSample(0); Evidence.positiveProbeRawValue = sample.Value; Evidence.positiveProbeEvents = sample.Count;
                long emptyEvents = Evidence.emptyProbeSamples > 0 ? Evidence.emptyProbeEvents : 0;
                long emptyValue = Evidence.emptyProbeSamples > 0 ? Evidence.emptyProbeRawValue : 0;
                Evidence.eventsVerified = sample.Count > emptyEvents;
                Evidence.bytesVerified = Evidence.eventsVerified && recorder.UnitType == ProfilerMarkerDataUnit.Bytes && sample.Value - emptyValue >= 4096;
                if (!Evidence.eventsVerified) Evidence.failure = "GC.Alloc did not synchronously observe the known allocation event.";
                else if (!Evidence.bytesVerified) Evidence.failure = "Allocation events verified, but synchronous bytes are unavailable: unit=" + Evidence.unit + ";probeValue=" + sample.Value + ".";
            }
            catch (Exception error) { Evidence.failure = error.GetType().Name + ": " + error.Message; }
        }
        public void Begin()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId) throw new InvalidOperationException("GC.Alloc scope must run on its verified initialization thread.");
            if (!Evidence.eventsVerified) return;
            recorder.Reset(); recorder.Start(); running = true;
        }
        public Sample End()
        {
            var result = new Sample { managedBytes = -1, allocationEvents = -1 };
            if (!running) return result;
            recorder.Stop(); running = false;
            // Even after a valid probe, absent samples remain missing, not a reported zero-byte result.
            if (recorder.Count == 0) return result;
            var sample = recorder.GetSample(0); result.hadSample = true;
            if (Evidence.eventsVerified) result.allocationEvents = sample.Count;
            if (Evidence.bytesVerified) result.managedBytes = sample.Value;
            return result;
        }
        public void Dispose() { if (created) { recorder.Dispose(); created = false; running = false; } }
    }
}
