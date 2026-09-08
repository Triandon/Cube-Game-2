using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ThreadedChunkWorker
{
    private readonly object reqLock = new object();
    private readonly object resLock = new object();

    private Queue<ChunkGenRequest> requestQueue = new Queue<ChunkGenRequest>();
    private Queue<ChunkGenResult> resultQueue = new Queue<ChunkGenResult>();

    private Thread[] workers;
    private volatile bool running = false;

    public ThreadedChunkWorker(int numThreads)
    {
        workers = new Thread[numThreads];
    }

    public void Start()
    {
        running = true;

        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = new Thread(WorkerLoop);
            workers[i].IsBackground = true;
            workers[i].Start();
        }
    }

    public void Stop()
    {
        running = false;

        lock (reqLock)
        {
            Monitor.PulseAll(reqLock);
        }

        foreach (var w in workers)
            w?.Join();
    }

    public void EnqueueRequest(ChunkGenRequest request)
    {
        lock (reqLock)
        {
            requestQueue.Enqueue(request);
            Monitor.Pulse(reqLock);
        }
    }

    public bool TryDequeueResult(out ChunkGenResult result)
    {
        lock (resLock)
        {
            if (resultQueue.Count > 0)
            {
                result = resultQueue.Dequeue();
                return true;
            }
        }

        result = null;
        return false;
    }

    private void WorkerLoop()
    {
        while (running)
        {
            ChunkGenRequest req = null;

            // ---- Get job ----
            lock (reqLock)
            {
                while (running && requestQueue.Count == 0)
                    Monitor.Wait(reqLock);

                if (!running)
                    return;

                req = requestQueue.Dequeue();
            }

            // ---- Process job OUTSIDE the lock ----
            ChunkGenResult res = ThreadedChunkProcessor.ProcessRequest(req);

            // ---- Store result ----
            lock (resLock)
            {
                resultQueue.Enqueue(res);
            }
        }
    }
}
