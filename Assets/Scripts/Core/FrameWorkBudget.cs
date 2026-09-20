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
        [SerializeField, Range(0f, 0.5f)] private float frameTimeReserveFraction = 0.023f;
        [SerializeField, Range(0f, 1f)] private float initialBudgetFraction = 0.25f;
        [SerializeField, Range(0f, 1f)] private float minimumBudgetFraction = 0.01f;
        [SerializeField, Min(0.1f)] private float budgetAdjustmentInterval = 0.25f;
        [SerializeField, Min(0.01f)] private float budgetIncreaseMs = 0.5f;
        [SerializeField, Range(0f, 1f)] private float availableHeadroomUsage = 0.5f;
        [SerializeField, Range(0.1f, 0.99f)] private float budgetDecreaseMultiplier = 0.75f;
        [SerializeField, Min(0f)] private float initialGraceSeconds = 15f;
        [SerializeField, Min(1f)] private float graceBudgetMultiplier = 2f;
        [Header("Determs if the system should be on/off, on if its on, off means its off!")]
        [SerializeField] private bool dynamicChunkRendering = true;
        
        private long startTimeStamp;
        private double budgetMs;
        private bool unlimited;
        private double smoothedFrameMs;
        private double adaptiveBudgetMs;
        private float budgetAdjustmentTimer;
        private float graceUntil;
        private bool hasPendingWork;
        public double BudgetMs => budgetMs;
        public double ElapsedMs => GetElapsedMilliseconds();
        public double RemainingMs => unlimited ? double.PositiveInfinity : 
            Math.Max(0.0, budgetMs - ElapsedMs);
        
        public bool IsGraceActive => Time.unscaledTime < graceUntil;

        public bool hasTimeRemaining => unlimited || ElapsedMs < budgetMs;
        
        public bool HasTimeRemaining(double cumulativeBudgetFraction)
        {
            if (unlimited)
                return true;

            double fraction = Math.Max(0.0, Math.Min(1.0, cumulativeBudgetFraction));
            return ElapsedMs < budgetMs * fraction;
        }

        private Settings settings;

        void Awake()
        {
            settings = Settings.Instance;
            graceUntil = Time.unscaledTime + initialGraceSeconds;
        }

        void Update()
        {
            int targetFps = settings != null ? settings.minTargetedFps : 32;
            double previousFrameMs = Time.unscaledDeltaTime * 1000.0;
            BeginFrame(targetFps, previousFrameMs, frameTimeReserveFraction,
                dynamicChunkRendering);
        }

        public void BeginFrame(int targetFps, double previousFrameMs, double reserveFraction,
            bool enabled)
        {
            startTimeStamp = Stopwatch.GetTimestamp();
            unlimited = !enabled;

            if (unlimited)
            {
                budgetMs = double.PositiveInfinity;
                return;
            }

            int clampedTargetFps = Math.Max(1, targetFps);
            double clampedReserveFraction = Math.Max(0.0, Math.Min(0.5, reserveFraction));

            double targetedFrameMs = 1000.0 / clampedTargetFps;
            double increaseThresholdMs = targetedFrameMs / (1.0 + clampedReserveFraction);
            double minimumBudgetMs = targetedFrameMs * Math.Max(0.0, minimumBudgetFraction);
            double maximumBudgetMs = targetedFrameMs * (1.0 - clampedReserveFraction);
            
            smoothedFrameMs = smoothedFrameMs <= 0.0
                ? Math.Max(0.0, previousFrameMs)
                : smoothedFrameMs * 0.9 + Math.Max(0.0, previousFrameMs) * 0.1;

            if (adaptiveBudgetMs <= 0.0)
                adaptiveBudgetMs = targetedFrameMs * initialBudgetFraction;

            UpdateAdaptiveBudget(targetedFrameMs, increaseThresholdMs,
                minimumBudgetMs, maximumBudgetMs);
            adaptiveBudgetMs = Math.Max(minimumBudgetMs,
                Math.Min(maximumBudgetMs, adaptiveBudgetMs));
            budgetMs = adaptiveBudgetMs;

            if (IsGraceActive)
                budgetMs *= graceBudgetMultiplier;
        }
        
        public void SetHasPendingWork(bool value)
        {
            hasPendingWork = value;
        }

        public void BeginGrace(float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return;

            graceUntil = Mathf.Max(graceUntil, Time.unscaledTime + durationSeconds);
        }

        private double GetElapsedMilliseconds()
        {
            return (Stopwatch.GetTimestamp() - startTimeStamp) * 1000.0 / Stopwatch.Frequency;
        }

        private void UpdateAdaptiveBudget(double targetedFrameMs, double increaseThresholdMs,
            double minimumBudgetMs, double maximumBudgetMs)
        {
            budgetAdjustmentTimer += Time.unscaledDeltaTime;
            if (budgetAdjustmentTimer < budgetAdjustmentInterval)
                return;

            budgetAdjustmentTimer = 0f;
            if (!hasPendingWork || smoothedFrameMs > targetedFrameMs)
                adaptiveBudgetMs *= budgetDecreaseMultiplier;
            else if (smoothedFrameMs < increaseThresholdMs)
            {
                double availableHeadroomMs = increaseThresholdMs - smoothedFrameMs;
                double increaseMs = Math.Max(budgetIncreaseMs,
                    availableHeadroomMs * Math.Max(0.0, Math.Min(1.0, availableHeadroomUsage)));
                adaptiveBudgetMs += increaseMs;
            }
            
            adaptiveBudgetMs = Math.Max(minimumBudgetMs,
                Math.Min(maximumBudgetMs, adaptiveBudgetMs));
        }

    }
}
