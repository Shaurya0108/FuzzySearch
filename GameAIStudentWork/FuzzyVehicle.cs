using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using GameAI;

// All the Fuzz
using Tochas.FuzzyLogic;
using Tochas.FuzzyLogic.MembershipFunctions;
using Tochas.FuzzyLogic.Evaluators;
using Tochas.FuzzyLogic.Mergers;
using Tochas.FuzzyLogic.Defuzzers;
using Tochas.FuzzyLogic.Expressions;

namespace GameAICourse
{
    public class FuzzyVehicle : AIVehicle
    {
        // Fuzzy Set enumeration types for inputs and outputs
        enum FzOutputThrottle { Brake, Coast, Accelerate }
        enum FzOutputWheel { TurnLeft, Straight, TurnRight }

        // Input sets for vehicle state
        enum FzInputSpeed { Slow, Medium, Fast }
        enum FzInputDistanceFromPath { VeryLeft, Left, Center, Right, VeryRight }
        enum FzInputAngleToPath { VeryNegative, Negative, Neutral, Positive, VeryPositive }
        enum FzInputCurvature { SharpLeft, SlightLeft, Straight, SlightRight, SharpRight }
        enum FzInputLookAheadCurvature { SharpLeft, SlightLeft, Straight, SlightRight, SharpRight }

        // Fuzzy Set declarations
        FuzzySet<FzInputSpeed> fzSpeedSet;
        FuzzySet<FzInputDistanceFromPath> fzDistanceSet;
        FuzzySet<FzInputAngleToPath> fzAngleSet;
        FuzzySet<FzInputCurvature> fzCurvatureSet;
        FuzzySet<FzInputLookAheadCurvature> fzLookAheadCurvatureSet;

        FuzzySet<FzOutputThrottle> fzThrottleSet;
        FuzzyRuleSet<FzOutputThrottle> fzThrottleRuleSet;

        FuzzySet<FzOutputWheel> fzWheelSet;
        FuzzyRuleSet<FzOutputWheel> fzWheelRuleSet;

        FuzzyValueSet fzInputValueSet = new FuzzyValueSet();

        // These are used for debugging (see ApplyFuzzyRules() call in Update())
        FuzzyValueSet mergedThrottle = new FuzzyValueSet();
        FuzzyValueSet mergedWheel = new FuzzyValueSet();

        // Debug variables
        private float currentDistanceFromPath = 0f;
        private float currentAngleToPath = 0f;
        private float currentCurvature = 0f;
        private float lookAheadCurvature = 0f;
        private Vector3 lookAheadPoint;
        private float lookAheadDistance = 20f; // Look ahead distance for predicting curves

        private FuzzySet<FzInputSpeed> GetSpeedSet()
        {
            FuzzySet<FzInputSpeed> set = new FuzzySet<FzInputSpeed>();

            // Membership functions for speed
            // Slow: 0-30 km/h
            set.Set(FzInputSpeed.Slow, new ShoulderMembershipFunction(
                0f, new Coords(0f, 1f), new Coords(30f, 0f), 60f));

            // Medium: 20-60 km/h
            set.Set(FzInputSpeed.Medium, new TrapezoidMembershipFunction(
                new Coords(20f, 0f), new Coords(30f, 1f), new Coords(45f, 1f), new Coords(60f, 0f)));

            // Fast: 45+ km/h
            set.Set(FzInputSpeed.Fast, new ShoulderMembershipFunction(
                20f, new Coords(45f, 0f), new Coords(60f, 1f), 100f));

            return set;
        }

        private FuzzySet<FzInputDistanceFromPath> GetDistanceSet()
        {
            FuzzySet<FzInputDistanceFromPath> set = new FuzzySet<FzInputDistanceFromPath>();

            // Membership functions for distance from center of path
            // Negative values mean left of path, positive values mean right of path
            float maxDistance = 5f; // Maximum reasonable distance from center

            // VeryLeft: < -3m from center
            set.Set(FzInputDistanceFromPath.VeryLeft, new ShoulderMembershipFunction(
                -maxDistance, new Coords(-maxDistance, 1f), new Coords(-3f, 0f), maxDistance));

            // Left: -4m to -0.5m from center
            set.Set(FzInputDistanceFromPath.Left, new TrapezoidMembershipFunction(
                new Coords(-4f, 0f), new Coords(-3f, 1f), new Coords(-1f, 1f), new Coords(-0.5f, 0f)));

            // Center: -1.5m to 1.5m from center
            set.Set(FzInputDistanceFromPath.Center, new TrapezoidMembershipFunction(
                new Coords(-1.5f, 0f), new Coords(-0.5f, 1f), new Coords(0.5f, 1f), new Coords(1.5f, 0f)));

            // Right: 0.5m to 4m from center
            set.Set(FzInputDistanceFromPath.Right, new TrapezoidMembershipFunction(
                new Coords(0.5f, 0f), new Coords(1f, 1f), new Coords(3f, 1f), new Coords(4f, 0f)));

            // VeryRight: > 3m from center
            set.Set(FzInputDistanceFromPath.VeryRight, new ShoulderMembershipFunction(
                -maxDistance, new Coords(3f, 0f), new Coords(maxDistance, 1f), maxDistance));

            return set;
        }

        private FuzzySet<FzInputAngleToPath> GetAngleSet()
        {
            FuzzySet<FzInputAngleToPath> set = new FuzzySet<FzInputAngleToPath>();

            // Membership functions for angle between vehicle and path
            // Negative angles mean vehicle is facing left of path direction
            // Positive angles mean vehicle is facing right of path direction
            float maxAngle = 90f; // Maximum angle to consider

            // VeryNegative: < -45 degrees
            set.Set(FzInputAngleToPath.VeryNegative, new ShoulderMembershipFunction(
                -maxAngle, new Coords(-maxAngle, 1f), new Coords(-45f, 0f), maxAngle));

            // Negative: -60 to -10 degrees
            set.Set(FzInputAngleToPath.Negative, new TrapezoidMembershipFunction(
                new Coords(-60f, 0f), new Coords(-45f, 1f), new Coords(-20f, 1f), new Coords(-10f, 0f)));

            // Neutral: -20 to 20 degrees
            set.Set(FzInputAngleToPath.Neutral, new TrapezoidMembershipFunction(
                new Coords(-20f, 0f), new Coords(-10f, 1f), new Coords(10f, 1f), new Coords(20f, 0f)));

            // Positive: 10 to 60 degrees
            set.Set(FzInputAngleToPath.Positive, new TrapezoidMembershipFunction(
                new Coords(10f, 0f), new Coords(20f, 1f), new Coords(45f, 1f), new Coords(60f, 0f)));

            // VeryPositive: > 45 degrees
            set.Set(FzInputAngleToPath.VeryPositive, new ShoulderMembershipFunction(
                -maxAngle, new Coords(45f, 0f), new Coords(maxAngle, 1f), maxAngle));

            return set;
        }

        private FuzzySet<FzInputCurvature> GetCurvatureSet()
        {
            FuzzySet<FzInputCurvature> set = new FuzzySet<FzInputCurvature>();

            // Membership functions for path curvature
            // Negative values mean left curve, positive values mean right curve
            float maxCurvature = 0.1f; // Maximum curvature to consider

            // SharpLeft: < -0.05 curvature
            set.Set(FzInputCurvature.SharpLeft, new ShoulderMembershipFunction(
                -maxCurvature, new Coords(-maxCurvature, 1f), new Coords(-0.05f, 0f), maxCurvature));

            // SlightLeft: -0.08 to -0.01 curvature
            set.Set(FzInputCurvature.SlightLeft, new TrapezoidMembershipFunction(
                new Coords(-0.08f, 0f), new Coords(-0.05f, 1f), new Coords(-0.02f, 1f), new Coords(-0.01f, 0f)));

            // Straight: -0.02 to 0.02 curvature
            set.Set(FzInputCurvature.Straight, new TrapezoidMembershipFunction(
                new Coords(-0.02f, 0f), new Coords(-0.01f, 1f), new Coords(0.01f, 1f), new Coords(0.02f, 0f)));

            // SlightRight: 0.01 to 0.08 curvature
            set.Set(FzInputCurvature.SlightRight, new TrapezoidMembershipFunction(
                new Coords(0.01f, 0f), new Coords(0.02f, 1f), new Coords(0.05f, 1f), new Coords(0.08f, 0f)));

            // SharpRight: > 0.05 curvature
            set.Set(FzInputCurvature.SharpRight, new ShoulderMembershipFunction(
                -maxCurvature, new Coords(0.05f, 0f), new Coords(maxCurvature, 1f), maxCurvature));

            return set;
        }

        private FuzzySet<FzInputLookAheadCurvature> GetLookAheadCurvatureSet()
        {
            FuzzySet<FzInputLookAheadCurvature> set = new FuzzySet<FzInputLookAheadCurvature>();

            // Membership functions for look-ahead path curvature
            // Negative values mean left curve, positive values mean right curve
            float maxCurvature = 0.1f; // Maximum curvature to consider

            // SharpLeft: < -0.05 curvature
            set.Set(FzInputLookAheadCurvature.SharpLeft, new ShoulderMembershipFunction(
                -maxCurvature, new Coords(-maxCurvature, 1f), new Coords(-0.05f, 0f), maxCurvature));

            // SlightLeft: -0.08 to -0.01 curvature
            set.Set(FzInputLookAheadCurvature.SlightLeft, new TrapezoidMembershipFunction(
                new Coords(-0.08f, 0f), new Coords(-0.05f, 1f), new Coords(-0.02f, 1f), new Coords(-0.01f, 0f)));

            // Straight: -0.02 to 0.02 curvature
            set.Set(FzInputLookAheadCurvature.Straight, new TrapezoidMembershipFunction(
                new Coords(-0.02f, 0f), new Coords(-0.01f, 1f), new Coords(0.01f, 1f), new Coords(0.02f, 0f)));

            // SlightRight: 0.01 to 0.08 curvature
            set.Set(FzInputLookAheadCurvature.SlightRight, new TrapezoidMembershipFunction(
                new Coords(0.01f, 0f), new Coords(0.02f, 1f), new Coords(0.05f, 1f), new Coords(0.08f, 0f)));

            // SharpRight: > 0.05 curvature
            set.Set(FzInputLookAheadCurvature.SharpRight, new ShoulderMembershipFunction(
                -maxCurvature, new Coords(0.05f, 0f), new Coords(maxCurvature, 1f), maxCurvature));

            return set;
        }

        private FuzzySet<FzOutputThrottle> GetThrottleSet()
        {
            FuzzySet<FzOutputThrottle> set = new FuzzySet<FzOutputThrottle>();

            // Membership functions for throttle output
            // Brake: -1.0 to -0.1
            set.Set(FzOutputThrottle.Brake, new ShoulderMembershipFunction(
                -1.0f, new Coords(-1.0f, 1f), new Coords(-0.1f, 0f), 1.0f));

            // Coast: -0.3 to 0.3
            set.Set(FzOutputThrottle.Coast, new TrapezoidMembershipFunction(
                new Coords(-0.3f, 0f), new Coords(-0.1f, 1f), new Coords(0.1f, 1f), new Coords(0.3f, 0f)));

            // Accelerate: 0.1 to 1.0
            set.Set(FzOutputThrottle.Accelerate, new ShoulderMembershipFunction(
                -1.0f, new Coords(0.1f, 0f), new Coords(1.0f, 1f), 1.0f));

            return set;
        }

        private FuzzySet<FzOutputWheel> GetWheelSet()
        {
            FuzzySet<FzOutputWheel> set = new FuzzySet<FzOutputWheel>();

            // Membership functions for steering output
            // TurnLeft: -1.0 to -0.1
            set.Set(FzOutputWheel.TurnLeft, new ShoulderMembershipFunction(
                -1.0f, new Coords(-1.0f, 1f), new Coords(-0.1f, 0f), 1.0f));

            // Straight: -0.3 to 0.3
            set.Set(FzOutputWheel.Straight, new TrapezoidMembershipFunction(
                new Coords(-0.3f, 0f), new Coords(-0.1f, 1f), new Coords(0.1f, 1f), new Coords(0.3f, 0f)));

            // TurnRight: 0.1 to 1.0
            set.Set(FzOutputWheel.TurnRight, new ShoulderMembershipFunction(
                -1.0f, new Coords(0.1f, 0f), new Coords(1.0f, 1f), 1.0f));

            return set;
        }

        private FuzzyRule<FzOutputThrottle>[] GetThrottleRules()
        {
            FuzzyRule<FzOutputThrottle>[] rules =
            {
                // Speed-based rules
                If(FzInputSpeed.Slow).Then(FzOutputThrottle.Accelerate),
                If(FzInputSpeed.Medium).Then(FzOutputThrottle.Accelerate),
                If(FzInputSpeed.Fast).Then(FzOutputThrottle.Coast),

                // Curvature-based throttle rules
                If(And(FzInputSpeed.Fast, FzInputCurvature.SharpLeft)).Then(FzOutputThrottle.Brake),
                If(And(FzInputSpeed.Fast, FzInputCurvature.SharpRight)).Then(FzOutputThrottle.Brake),
                If(And(FzInputSpeed.Fast, FzInputCurvature.SlightLeft)).Then(FzOutputThrottle.Coast),
                If(And(FzInputSpeed.Fast, FzInputCurvature.SlightRight)).Then(FzOutputThrottle.Coast),
                
                // Look-ahead curvature throttle rules
                If(And(FzInputSpeed.Fast, FzInputLookAheadCurvature.SharpLeft)).Then(FzOutputThrottle.Brake),
                If(And(FzInputSpeed.Fast, FzInputLookAheadCurvature.SharpRight)).Then(FzOutputThrottle.Brake),
                If(And(FzInputSpeed.Medium, FzInputLookAheadCurvature.SharpLeft)).Then(FzOutputThrottle.Coast),
                If(And(FzInputSpeed.Medium, FzInputLookAheadCurvature.SharpRight)).Then(FzOutputThrottle.Coast),
                
                // Angle-based throttle rules - slow down when facing away from track
                If(FzInputAngleToPath.VeryNegative).Then(FzOutputThrottle.Brake),
                If(FzInputAngleToPath.VeryPositive).Then(FzOutputThrottle.Brake),
                If(And(FzInputSpeed.Fast, FzInputAngleToPath.Negative)).Then(FzOutputThrottle.Coast),
                If(And(FzInputSpeed.Fast, FzInputAngleToPath.Positive)).Then(FzOutputThrottle.Coast),

                // Distance-based throttle rules - slow down when far from center
                If(And(FzInputSpeed.Fast, FzInputDistanceFromPath.VeryLeft)).Then(FzOutputThrottle.Brake),
                If(And(FzInputSpeed.Fast, FzInputDistanceFromPath.VeryRight)).Then(FzOutputThrottle.Brake),
                If(And(FzInputSpeed.Medium, FzInputDistanceFromPath.VeryLeft)).Then(FzOutputThrottle.Coast),
                If(And(FzInputSpeed.Medium, FzInputDistanceFromPath.VeryRight)).Then(FzOutputThrottle.Coast),

                // Ideal condition for full acceleration
                If(And(FzInputDistanceFromPath.Center, FzInputAngleToPath.Neutral,
                       FzInputCurvature.Straight, FzInputLookAheadCurvature.Straight)).Then(FzOutputThrottle.Accelerate)
            };

            return rules;
        }

        private FuzzyRule<FzOutputWheel>[] GetWheelRules()
        {
            FuzzyRule<FzOutputWheel>[] rules =
            {
                // Distance-based steering rules
                If(FzInputDistanceFromPath.VeryLeft).Then(FzOutputWheel.TurnRight),
                If(FzInputDistanceFromPath.Left).Then(FzOutputWheel.TurnRight),
                If(FzInputDistanceFromPath.Center).Then(FzOutputWheel.Straight),
                If(FzInputDistanceFromPath.Right).Then(FzOutputWheel.TurnLeft),
                If(FzInputDistanceFromPath.VeryRight).Then(FzOutputWheel.TurnLeft),
                
                // Angle-based steering rules
                If(FzInputAngleToPath.VeryNegative).Then(FzOutputWheel.TurnRight),
                If(FzInputAngleToPath.Negative).Then(FzOutputWheel.TurnRight),
                If(FzInputAngleToPath.Neutral).Then(FzOutputWheel.Straight),
                If(FzInputAngleToPath.Positive).Then(FzOutputWheel.TurnLeft),
                If(FzInputAngleToPath.VeryPositive).Then(FzOutputWheel.TurnLeft),
                
                // Curvature prediction rules
                If(FzInputCurvature.SharpLeft).Then(FzOutputWheel.TurnLeft),
                If(FzInputCurvature.SlightLeft).Then(FzOutputWheel.TurnLeft),
                If(FzInputCurvature.Straight).Then(FzOutputWheel.Straight),
                If(FzInputCurvature.SlightRight).Then(FzOutputWheel.TurnRight),
                If(FzInputCurvature.SharpRight).Then(FzOutputWheel.TurnRight),
                
                // Look-ahead curvature steering rules
                If(FzInputLookAheadCurvature.SharpLeft).Then(FzOutputWheel.TurnLeft),
                If(FzInputLookAheadCurvature.SlightLeft).Then(FzOutputWheel.TurnLeft),
                If(FzInputLookAheadCurvature.SlightRight).Then(FzOutputWheel.TurnRight),
                If(FzInputLookAheadCurvature.SharpRight).Then(FzOutputWheel.TurnRight),
                
                // Combined rules for complex situations
                
                // Turn into the curve when off-center
                If(And(FzInputDistanceFromPath.VeryLeft, FzInputCurvature.SharpLeft)).Then(FzOutputWheel.TurnLeft),
                If(And(FzInputDistanceFromPath.VeryRight, FzInputCurvature.SharpRight)).Then(FzOutputWheel.TurnRight),
                
                // Stronger correction when both angle and distance are off
                If(And(FzInputDistanceFromPath.VeryLeft, FzInputAngleToPath.VeryNegative)).Then(FzOutputWheel.TurnRight),
                If(And(FzInputDistanceFromPath.VeryRight, FzInputAngleToPath.VeryPositive)).Then(FzOutputWheel.TurnLeft),
                
                // Corrective counter-steering when skidding
                If(And(FzInputDistanceFromPath.VeryLeft, FzInputAngleToPath.VeryPositive)).Then(FzOutputWheel.TurnLeft),
                If(And(FzInputDistanceFromPath.VeryRight, FzInputAngleToPath.VeryNegative)).Then(FzOutputWheel.TurnRight)
            };

            return rules;
        }

        private FuzzyRuleSet<FzOutputThrottle> GetThrottleRuleSet(FuzzySet<FzOutputThrottle> throttle)
        {
            var rules = this.GetThrottleRules();
            return new FuzzyRuleSet<FzOutputThrottle>(throttle, rules);
        }

        private FuzzyRuleSet<FzOutputWheel> GetWheelRuleSet(FuzzySet<FzOutputWheel> wheel)
        {
            var rules = this.GetWheelRules();
            return new FuzzyRuleSet<FzOutputWheel>(wheel, rules);
        }

        protected override void Awake()
        {
            base.Awake();

            StudentName = "Shaurya Dwivedi";

            IsPlayer = false;
        }

        protected override void Start()
        {
            base.Start();

            // Initialize Fuzzy sets
            fzSpeedSet = this.GetSpeedSet();
            fzDistanceSet = this.GetDistanceSet();
            fzAngleSet = this.GetAngleSet();
            fzCurvatureSet = this.GetCurvatureSet();
            fzLookAheadCurvatureSet = this.GetLookAheadCurvatureSet();

            fzThrottleSet = this.GetThrottleSet();
            fzThrottleRuleSet = this.GetThrottleRuleSet(fzThrottleSet);

            fzWheelSet = this.GetWheelSet();
            fzWheelRuleSet = this.GetWheelRuleSet(fzWheelSet);
        }

        System.Text.StringBuilder strBldr = new System.Text.StringBuilder();

        // Calculate the approximate curvature at a point on the path
        private float CalculateCurvature(float distanceAhead)
        {
            // Get current path distance
            float currentDist = pathTracker.distanceTravelled;

            // Get distance ahead and behind on path
            float aheadDist = Mathf.Min(currentDist + distanceAhead, pathTracker.MaxPathDistance);
            float behindDist = Mathf.Max(currentDist - distanceAhead, 0);

            // Sample path at these distances
            Vector3 currentDir = pathTracker.closestPointDirectionOnPath;

            // We need to get directions at ahead and behind points
            // Since we don't have direct access, we'll estimate using path directions
            // Store current position and temporarily move pathTracker to get directions
            Vector3 originalPos = transform.position;
            float originalDist = pathTracker.distanceTravelled;

            // Get direction at ahead point
            pathTracker.ResetToDistance(aheadDist);
            Vector3 aheadDir = pathTracker.closestPointDirectionOnPath;

            // Get direction at behind point
            pathTracker.ResetToDistance(behindDist);
            Vector3 behindDir = pathTracker.closestPointDirectionOnPath;

            // Restore original position
            pathTracker.ResetToDistance(originalDist);

            // Calculate the cross product magnitude and normalize it
            // Cross product direction tells us if we're turning left or right
            Vector3 cross = Vector3.Cross(behindDir, aheadDir);
            float crossMag = cross.magnitude;

            // Sign determines left vs right curve
            float sign = Vector3.Dot(cross, Vector3.up) > 0 ? 1f : -1f;

            return sign * crossMag / (2f * distanceAhead);
        }

        // Gets a point ahead on the path for look-ahead curvature
        private Vector3 GetLookAheadPoint(float distance)
        {
            // Calculate the distance along the path
            float currentDist = pathTracker.distanceTravelled;
            float aheadDist = Mathf.Min(currentDist + distance, pathTracker.MaxPathDistance);

            // Store current path tracking state
            float originalDist = pathTracker.distanceTravelled;

            // Move path tracker temporarily to the look-ahead distance
            pathTracker.ResetToDistance(aheadDist);

            // Get the point at that distance
            Vector3 lookAheadPoint = pathTracker.closestPointOnPath;

            // Restore original path tracking
            pathTracker.ResetToDistance(originalDist);

            return lookAheadPoint;
        }

        override protected void Update()
        {
            // Calculate the current distance from the path centerline
            // Positive value means the vehicle is to the right of the centerline
            // Negative value means the vehicle is to the left of the centerline
            Vector3 vehicleToPathVector = pathTracker.closestPointOnPath - transform.position;
            Vector3 pathNormal = Vector3.Cross(pathTracker.closestPointDirectionOnPath, Vector3.up).normalized;
            currentDistanceFromPath = Vector3.Dot(vehicleToPathVector, pathNormal);

            // Calculate the angle between vehicle forward and path direction
            // Positive value means the vehicle is pointing to the right of the path
            // Negative value means the vehicle is pointing to the left of the path
            Vector3 pathDir = pathTracker.closestPointDirectionOnPath;
            Vector3 vehicleDir = transform.forward;
            float dotProduct = Vector3.Dot(pathDir, vehicleDir);
            float crossProductY = Vector3.Cross(pathDir, vehicleDir).y;
            float angle = Mathf.Acos(Mathf.Clamp(dotProduct, -1f, 1f)) * Mathf.Rad2Deg;
            currentAngleToPath = crossProductY > 0 ? angle : -angle;

            // Calculate current path curvature
            currentCurvature = CalculateCurvature(5f); // 5 meters distance for curvature calculation

            // Calculate look-ahead curvature (for upcoming turns)
            lookAheadPoint = GetLookAheadPoint(lookAheadDistance);
            // Since we already moved ahead, we can just check the current curvature
            lookAheadCurvature = CalculateCurvature(5f);

            // Fuzzify the input values
            fzSpeedSet.Evaluate(Speed, fzInputValueSet);
            fzDistanceSet.Evaluate(currentDistanceFromPath, fzInputValueSet);
            fzAngleSet.Evaluate(currentAngleToPath, fzInputValueSet);
            fzCurvatureSet.Evaluate(currentCurvature, fzInputValueSet);
            fzLookAheadCurvatureSet.Evaluate(lookAheadCurvature, fzInputValueSet);

            // Apply fuzzy rules to determine throttle and steering
            ApplyFuzzyRules<FzOutputThrottle, FzOutputWheel>(
                fzThrottleRuleSet,
                fzWheelRuleSet,
                fzInputValueSet,
                out var throttleRuleOutput,
                out var wheelRuleOutput,
                ref mergedThrottle,
                ref mergedWheel
            );

            // Use vizText for debugging output
            if (vizText != null)
            {
                strBldr.Clear();
                strBldr.AppendLine($"Speed: {Speed:F1} km/h");
                strBldr.AppendLine($"Distance from path: {currentDistanceFromPath:F2} m");
                strBldr.AppendLine($"Angle to path: {currentAngleToPath:F1}°");
                strBldr.AppendLine($"Curvature: {currentCurvature:F4}");
                strBldr.AppendLine($"Look-ahead curvature: {lookAheadCurvature:F4}");
                strBldr.AppendLine($"Throttle: {Throttle:F2}");
                strBldr.AppendLine($"Steering: {Steering:F2}");

                // Print fuzzy input sets
                strBldr.AppendLine("\nFuzzy Input Values:");
                AIVehicle.DiagnosticPrintFuzzyValueSet<FzInputSpeed>(fzInputValueSet, strBldr);
                AIVehicle.DiagnosticPrintFuzzyValueSet<FzInputDistanceFromPath>(fzInputValueSet, strBldr);
                AIVehicle.DiagnosticPrintFuzzyValueSet<FzInputAngleToPath>(fzInputValueSet, strBldr);
                AIVehicle.DiagnosticPrintFuzzyValueSet<FzInputCurvature>(fzInputValueSet, strBldr);
                AIVehicle.DiagnosticPrintFuzzyValueSet<FzInputLookAheadCurvature>(fzInputValueSet, strBldr);

                // Print fuzzy rule outputs
                strBldr.AppendLine("\nFuzzy Rule Outputs:");
                AIVehicle.DiagnosticPrintRuleSet<FzOutputThrottle>(fzThrottleRuleSet, throttleRuleOutput, strBldr);
                AIVehicle.DiagnosticPrintRuleSet<FzOutputWheel>(fzWheelRuleSet, wheelRuleOutput, strBldr);

                vizText.text = strBldr.ToString();
            }

            // Keep the base Update call at the end
            base.Update();
        }
    }
}
