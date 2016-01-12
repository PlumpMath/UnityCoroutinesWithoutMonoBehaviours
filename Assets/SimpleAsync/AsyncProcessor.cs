using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using UnityEngine;

public class AsyncProcessor
{
    public event Action<Exception> OnException = delegate {};

    readonly List<CoroutineInfo> _newWorkers = new List<CoroutineInfo>();
    readonly LinkedList<CoroutineInfo> _workers = new LinkedList<CoroutineInfo>();

    public bool IsBlocking
    {
        get
        {
            return _workers.Where(x => x.IsBlocking).Any() || _newWorkers.Where(x => x.IsBlocking).Any();
        }
    }

    public string StatusTitle
    {
        get
        {
            return _workers.Where(x => x.IsBlocking).Select(x => x.StatusTitle).FirstOrDefault();
        }
    }

    public bool IsRunning
    {
        get
        {
            return _workers.Any() || _newWorkers.Any();
        }
    }

    void AdvanceFrameAll()
    {
        var currentNode = _workers.First;

        while (currentNode != null)
        {
            var next = currentNode.Next;
            var worker = currentNode.Value;

            try
            {
                worker.CoRoutine.Pump();
                worker.IsFinished = worker.CoRoutine.IsDone;
            }
            catch (Exception e)
            {
                worker.IsFinished = true;
                Debug.LogException(e);
                OnException(e);
            }

            if (worker.IsFinished)
            {
                _workers.Remove(currentNode);
            }

            currentNode = next;
        }
    }

    public void Tick()
    {
        AddNewWorkers(); //Adding newworkers waiting to be added

        if (!_workers.Any())
        {
            return;
        }

        AdvanceFrameAll();
        AddNewWorkers(); //Added any workers that might have been added when the last worker was removed
    }

    public IEnumerator Process(IEnumerator process, string statusTitle = null, bool isBlocking = true, Action<Exception> exceptionHandler = null)
    {
        return ProcessInternal(process, statusTitle, isBlocking, exceptionHandler);
    }

    public IEnumerator Process<T>(IEnumerator process, string statusTitle = null, bool isBlocking = true, Action<Exception> exceptionHandler = null)
    {
        return ProcessInternal(process, statusTitle, isBlocking, exceptionHandler);
    }

    IEnumerator ProcessInternal(
        IEnumerator process, string statusTitle, bool isBlocking, Action<Exception> exceptionHandler)
    {
        var data = new CoroutineInfo()
        {
            CoRoutine = new CoRoutine(process),
            IsBlocking = isBlocking,
            StatusTitle = statusTitle,
        };

        _newWorkers.Add(data);

        return WaitUntilFinished(data);
    }

    IEnumerator WaitUntilFinished(CoroutineInfo workerData)
    {
        while (!workerData.IsFinished)
        {
            yield return null;
        }
    }

    void AddNewWorkers()
    {
        foreach (var worker in _newWorkers)
        {
            _workers.AddLast(worker);
        }
        _newWorkers.Clear();
    }

    class CoroutineInfo
    {
        public CoRoutine CoRoutine;
        public string StatusTitle;
        public bool IsBlocking;
        public bool IsFinished;
    }
}
