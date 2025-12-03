using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class MainThreadDispatcher : MonoBehaviour
    {

        private static readonly Queue<Action> actions = new Queue<Action>();

        public static void Enqueue(Action action)
        {
            lock (actions)
            {
                actions.Enqueue(action);
            }
        }

        // Update is called once per frame
        void Update()
        {
            lock (actions)
            {
                while (actions.Count > 0)
                {
                    actions.Dequeue().Invoke();
                }
            }
        }
    }
}
