using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Core
{
    [DefaultExecutionOrder(-100)]
    public class FrameWorkBudget : MonoBehaviour
    {
        //Budgeting by using delta ms from frames
        [Header("Dynamic Frame Budgeting")]
        [SerializeField, Min(0.05f)] private float maximumFrameBudgetFraction = 0.25f;
        [SerializeField, Min(0f)] private float frameBudgetSafetyMarginMs = 1f;
        [SerializeField, Min(0.05f)] private float minimumFrameBudgetMs = 0.25f;
        [Header("Determs if the system should be on/off, on if its on, off means its off!")]
        [SerializeField] private bool dynamicChunkRendering = true;
        
        private long startTimeStamp;
        private double budgetMs;
        private bool unlimited;

        public double BudgetMs => budgetMs;
        public double ElapsedMs => GetElapsedMilliseconds();
        public double RemainingMs => unlimited ? double.PositiveInfinity : 
            Math.Max(0.0, budgetMs - ElapsedMs);

        public bool hasTimeRemaining => unlimited || ElapsedMs < budgetMs;

        private Settings settings;

        void Awake()
        {
            settings = Settings.Instance;
        }

        void Update()
        {
            int targetFps = settings != null ? settings.minTargetedFps : 32;
            double previousFrameMs = Time.unscaledDeltaTime * 1000.0;
            BeginFrame(targetFps, previousFrameMs, maximumFrameBudgetFraction,
                frameBudgetSafetyMarginMs, minimumFrameBudgetMs, dynamicChunkRendering);
        }

        public void BeginFrame(int targetFps, double previousFrameMs, double maxBudgetFraction,
            double safteyMarginMs, double minBudgetMs, bool enabled)
        {
            startTimeStamp = Stopwatch.GetTimestamp();
            unlimited = !enabled;

            if (unlimited)
            {
                budgetMs = double.PositiveInfinity;
                return;
            }

            int clampedTargetFps = Math.Max(1, targetFps);
            double clampedBudgetFraction = Math.Max(0.05, maxBudgetFraction);
            double clampedSafteyMarginMs = Math.Max(0.0, safteyMarginMs);
            double clampedMinBudgetsMs = Math.Max(0.0, minBudgetMs);

            double targetedFrameMs = 1000.0 / clampedTargetFps;
            double maxBudgetMs = targetedFrameMs * clampedBudgetFraction;
            double availableFrameMs = targetedFrameMs - Math.Max(0.0, previousFrameMs) - clampedSafteyMarginMs;

            budgetMs = Math.Max(clampedMinBudgetsMs, Math.Min(Math.Max(clampedMinBudgetsMs, maxBudgetMs),
                availableFrameMs));
        }

        private double GetElapsedMilliseconds()
        {
            return (Stopwatch.GetTimestamp() - startTimeStamp) * 1000.0 / Stopwatch.Frequency;
        }

    }
}
