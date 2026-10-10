using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SwapHunter
{
    // Reusable synchronous scope. Observation and cleanup deliberately occur outside the allocation window.
    public sealed class BoltAllocationMeasurement
    {
        public struct SynchronousSample
        {
            public long managedBytes, allocationEvents, productionCreated;
            public bool hadProfilerSample;
            public double callMilliseconds;
            public int successfulSpawnCalls, distinctRootIdentities, newlyObservedRootIdentities, activeRoots, observedObjects;
        }
        public struct CleanupObservation
        {
            public int destroyedRoots, retainedInactiveRoots, activeRoots, destroyedObjects, retainedInactiveObjects, activeObjects;
            public long productionRetired;
            public bool Complete => activeRoots == 0 && activeObjects == 0;
        }
        public struct RetiredMotionObservation { public int movedRoots, movedObjects; }
        readonly EnemyActor source;
        readonly SynchronousGcAllocationRecorder counter;
        readonly Vector3 origin;
        readonly Bolt[] bolts;
        readonly GameObject[] roots, objects;
        readonly Vector3[] directions, retiredRootPositions, retiredObjectPositions;
        readonly HashSet<int> seenRoots, batchRoots;
        int objectCount;
        float controlSink;
        bool cleanupRequested;
        long productionRetiredBefore;
        public int Count => bolts.Length;

        public BoltAllocationMeasurement(EnemyActor source, Vector3 origin, int count, SynchronousGcAllocationRecorder counter)
        {
            if (!source || count < 1) throw new ArgumentException("Allocation scope requires a real source and positive batch size.");
            this.source = source; this.origin = origin;
            this.counter = counter ?? throw new ArgumentNullException(nameof(counter));
            bolts = new Bolt[count]; roots = new GameObject[count]; objects = new GameObject[count * 16]; directions = new Vector3[count];
            retiredRootPositions = new Vector3[count]; retiredObjectPositions = new Vector3[objects.Length];
            seenRoots = new HashSet<int>(count * 32); batchRoots = new HashSet<int>(count);
            for (int i = 0; i < count; i++) { float angle = i * Mathf.PI * 2 / count; directions[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)); }
            Stopwatch.GetTimestamp(); // Initialize the timer before measured windows.
        }
        public SynchronousSample MeasureEmptyLoop()
        {
            counter.Begin(); long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < directions.Length; i++) controlSink += directions[i].sqrMagnitude;
            long end = Stopwatch.GetTimestamp(); var allocation = counter.End();
            return new SynchronousSample { managedBytes = allocation.managedBytes, allocationEvents = allocation.allocationEvents, hadProfilerSample = allocation.hadSample, callMilliseconds = (end - start) * 1000.0 / Stopwatch.Frequency };
        }
        public SynchronousSample MeasureSpawnBatch()
        {
            if (objectCount > 0 && !ObserveCleanup().Complete) throw new InvalidOperationException("Previous batch still has active objects.");
            Array.Clear(bolts, 0, bolts.Length); Array.Clear(roots, 0, roots.Length); Array.Clear(objects, 0, objects.Length);
            objectCount = 0; cleanupRequested = false; batchRoots.Clear();
            long productionCreatedBefore = Bolt.TotalCreated; productionRetiredBefore = Bolt.TotalRetired;
            counter.Begin(); long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < bolts.Length; i++) bolts[i] = Bolt.Spawn(source, origin, directions[i], 23, 5);
            long end = Stopwatch.GetTimestamp(); var allocation = counter.End();
            var sample = new SynchronousSample { managedBytes = allocation.managedBytes, allocationEvents = allocation.allocationEvents, hadProfilerSample = allocation.hadSample, callMilliseconds = (end - start) * 1000.0 / Stopwatch.Frequency };
            // These inspections are outside the scope: do not attribute observer collections to Bolt.Spawn.
            for (int i = 0; i < bolts.Length; i++)
            {
                if (!bolts[i]) continue;
                sample.successfulSpawnCalls++; roots[i] = bolts[i].gameObject;
                int id = roots[i].GetInstanceID();
                if (!batchRoots.Add(id)) continue;
                sample.distinctRootIdentities++;
                if (seenRoots.Add(id)) sample.newlyObservedRootIdentities++;
                if (roots[i].activeInHierarchy) sample.activeRoots++;
                ObserveHierarchy(roots[i].transform);
            }
            sample.observedObjects = objectCount; sample.productionCreated = Bolt.TotalCreated - productionCreatedBefore; return sample;
        }
        void ObserveHierarchy(Transform node)
        {
            if (objectCount == objects.Length) throw new InvalidOperationException("Projectile hierarchy exceeds preallocated observer capacity; observation cannot be truncated.");
            objects[objectCount++] = node.gameObject;
            for (int i = 0; i < node.childCount; i++) ObserveHierarchy(node.GetChild(i));
        }
        public void RequestCleanup()
        {
            if (cleanupRequested) return; cleanupRequested = true;
            for (int i = 0; i < bolts.Length; i++)
                if (bolts[i])
                {
                    bolts[i].Retire();
                }
        }
        public void CaptureRetiredPositions()
        {
            if (!cleanupRequested || !ObserveCleanup().Complete) throw new InvalidOperationException("Retired-motion observation requires completed cleanup.");
            for (int i = 0; i < roots.Length; i++) if (roots[i]) retiredRootPositions[i] = roots[i].transform.position;
            for (int i = 0; i < objectCount; i++) if (objects[i]) retiredObjectPositions[i] = objects[i].transform.position;
        }
        public RetiredMotionObservation ObserveRetiredMotion()
        {
            var result = new RetiredMotionObservation();
            for (int i = 0; i < roots.Length; i++) if (roots[i] && (roots[i].transform.position - retiredRootPositions[i]).sqrMagnitude > .000001f) result.movedRoots++;
            for (int i = 0; i < objectCount; i++) if (objects[i] && (objects[i].transform.position - retiredObjectPositions[i]).sqrMagnitude > .000001f) result.movedObjects++;
            return result;
        }
        public CleanupObservation ObserveCleanup()
        {
            var result = new CleanupObservation { productionRetired = Bolt.TotalRetired - productionRetiredBefore };
            foreach (var root in roots)
            {
                if (!root) result.destroyedRoots++;
                else if (root.activeInHierarchy) result.activeRoots++;
                else result.retainedInactiveRoots++;
            }
            for (int i = 0; i < objectCount; i++)
            {
                if (!objects[i]) result.destroyedObjects++;
                else if (objects[i].activeInHierarchy) result.activeObjects++;
                else result.retainedInactiveObjects++;
            }
            return result;
        }
        public static int CountActiveBolts(Transform effects, List<Bolt> buffer)
        {
            buffer.Clear(); effects.GetComponentsInChildren<Bolt>(false, buffer);
            int count = 0; foreach (var bolt in buffer) if (bolt && bolt.isActiveAndEnabled) count++;
            return count;
        }
    }
}
