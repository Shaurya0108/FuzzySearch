using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Random = UnityEngine.Random;

using GameAI;

// All the Fuzz
using Tochas.FuzzyLogic;
using Tochas.FuzzyLogic.MembershipFunctions;
using Tochas.FuzzyLogic.Evaluators;
using Tochas.FuzzyLogic.Mergers;
using Tochas.FuzzyLogic.Defuzzers;
using Tochas.FuzzyLogic.Expressions;


namespace GameAI
{

    public partial class FLBall3DAgent : MonoBehaviour
    {

        public GameObject ball;

        GameObject topCenterOfHead;

        Rigidbody m_BallRb;


        // Some balance head rotation smoothing control

        [SerializeField]
        protected float RotationSmoothTime = 2f;
        protected Quaternion quaternionDeriv;


        // Fuzzy Stuff

        enum FzInputBallPosX { Negative, Zero, Positive };
        enum FzInputBallPosZ { Negative, Zero, Positive };

        enum FzInputBallVelX { Negative, Zero, Positive };
        enum FzInputBallVelZ { Negative, Zero, Positive };

        enum FzOutputHeadRotX { RotNegDir, NoRot, RotPosDir };
        enum FzOutputHeadRotZ { RotNegDir, NoRot, RotPosDir };

        FuzzySet<FzInputBallPosX> inputBallPosXSet;
        FuzzySet<FzInputBallPosZ> inputBallPosZSet;

        FuzzySet<FzInputBallVelX> inputBallVelXSet;
        FuzzySet<FzInputBallVelZ> inputBallVelZSet;

        FuzzySet<FzOutputHeadRotX> outputHeadRotXSet;
        FuzzySet<FzOutputHeadRotZ> outputHeadRotZSet;

        FuzzyRuleSet<FzOutputHeadRotX> headRotXRuleSet;
        FuzzyRuleSet<FzOutputHeadRotZ> headRotZRuleSet;

        FuzzyValueSet fzInputValueSet = new FuzzyValueSet();

        FuzzyValueSet mergedHeadRotX = new FuzzyValueSet();
        FuzzyValueSet mergedHeadRotZ = new FuzzyValueSet();


        // balance guy's head half-width
        const float halfWidth = 2.5f;


        void Awake()
        {
            m_BallRb = ball.GetComponent<Rigidbody>();

            m_BallRb.sleepThreshold = 0.1f;
        }


        // Helper to create common DoM Functions for a Fuzzy Set with 3 states
        private FuzzySet<T> GetNormThreeWaySet<T>(
            T v1, T v2, T v3,
            float lowMinValue = -1f,       // 1
            float minValue = -1f,          // 2
            float lowMiddleValue = 0.0f,   // 3
            float highMiddleValue = 0.0f,  // 4
            float maxValue = 1f,           // 5
            float highMaxValue = 1f        // 6
        ) where T : struct, IConvertible
        {
            // -----------\   /--\   /-----------
            //             \ /    \ /  
            //              \      \   
            //             / \    / \  
            //            /   \  /   \  
            // 1          2   3  4   5          6
            //

            IMembershipFunction leftFx = new ShoulderMembershipFunction(lowMinValue, new Coords(minValue, 1f), new Coords(lowMiddleValue, 0f), maxValue);

            IMembershipFunction midFx;

            if (lowMiddleValue == highMiddleValue)
                midFx = new TriangularMembershipFunction(new Coords(minValue, 0f), new Coords(lowMiddleValue, 1f), new Coords(maxValue, 0f));
            else
                midFx = new TrapezoidMembershipFunction(new Coords(minValue, 0f), new Coords(lowMiddleValue, 1f), new Coords(highMiddleValue, 1f), new Coords(maxValue, 0f));

            IMembershipFunction rightFx = new ShoulderMembershipFunction(minValue, new Coords(highMiddleValue, 0f), new Coords(maxValue, 1f), highMaxValue);

            FuzzySet<T> set = new FuzzySet<T>();
            set.Set(v1, leftFx);
            set.Set(v2, midFx);
            set.Set(v3, rightFx);
            return set;
        }


        const float deadZone = 0.01f;
        const float maxAdjust = 0.75f;

        private FuzzySet<FzInputBallPosX> GetBallPosXSet()
        {
            return GetNormThreeWaySet<FzInputBallPosX>(FzInputBallPosX.Negative, FzInputBallPosX.Zero, FzInputBallPosX.Positive,
                lowMinValue: -halfWidth,
                minValue: -halfWidth * maxAdjust,
                lowMiddleValue: -deadZone,
                highMiddleValue: deadZone,
                maxValue: halfWidth * maxAdjust,
                highMaxValue: halfWidth
                );
        }

        private FuzzySet<FzInputBallPosZ> GetBallPosZSet()
        {
            return GetNormThreeWaySet<FzInputBallPosZ>(FzInputBallPosZ.Negative, FzInputBallPosZ.Zero, FzInputBallPosZ.Positive,
                lowMinValue: -halfWidth,
                minValue: -halfWidth * maxAdjust,
                lowMiddleValue: -deadZone,
                highMiddleValue: deadZone,
                maxValue: halfWidth * maxAdjust,
                highMaxValue: halfWidth
                );
        }


        const float maxSpeed = 1f;

        private FuzzySet<FzInputBallVelX> GetBallVelXSet()
        {
            return GetNormThreeWaySet<FzInputBallVelX>(FzInputBallVelX.Negative, FzInputBallVelX.Zero, FzInputBallVelX.Positive,
                lowMinValue: -maxSpeed,
                minValue: -maxSpeed * maxAdjust,
                lowMiddleValue: -deadZone,
                highMiddleValue: deadZone,
                maxValue: maxSpeed * maxAdjust,
                highMaxValue: maxSpeed
                );
        }


        private FuzzySet<FzInputBallVelZ> GetBallVelZSet()
        {
            return GetNormThreeWaySet<FzInputBallVelZ>(FzInputBallVelZ.Negative, FzInputBallVelZ.Zero, FzInputBallVelZ.Positive,
                lowMinValue: -maxSpeed,
                minValue: -maxSpeed * maxAdjust,
                lowMiddleValue: -deadZone,
                highMiddleValue: deadZone,
                maxValue: maxSpeed * maxAdjust,
                highMaxValue: maxSpeed
                );
        }


        const float absMaxAngle = 20f;

        private FuzzySet<FzOutputHeadRotX> GetHeadRotXSet()
        {
            return GetNormThreeWaySet<FzOutputHeadRotX>(FzOutputHeadRotX.RotNegDir, FzOutputHeadRotX.NoRot, FzOutputHeadRotX.RotPosDir,
                lowMinValue: -absMaxAngle,
                minValue: -absMaxAngle,
                lowMiddleValue: 0f,
                highMiddleValue: 0f,
                maxValue: absMaxAngle,
                highMaxValue: absMaxAngle
                );
        }

        private FuzzySet<FzOutputHeadRotZ> GetHeadRotZSet()
        {
            return GetNormThreeWaySet<FzOutputHeadRotZ>(FzOutputHeadRotZ.RotNegDir, FzOutputHeadRotZ.NoRot, FzOutputHeadRotZ.RotPosDir,
                lowMinValue: -absMaxAngle,
                minValue: -absMaxAngle,
                lowMiddleValue: 0f,
                highMiddleValue: 0f,
                maxValue: absMaxAngle,
                highMaxValue: absMaxAngle
                );
        }



        private FuzzyRule<FzOutputHeadRotX>[] GetHeadRotXRules()
        {
            FuzzyRule<FzOutputHeadRotX>[] rules = new FuzzyRule<FzOutputHeadRotX>[]
            {
                If(FzInputBallPosZ.Negative).Then(FzOutputHeadRotX.RotPosDir),
                If(FzInputBallPosZ.Zero).Then(FzOutputHeadRotX.NoRot),
                If(FzInputBallPosZ.Positive).Then(FzOutputHeadRotX.RotNegDir),

                If(FzInputBallVelZ.Negative).Then(FzOutputHeadRotX.RotPosDir),
                If(FzInputBallVelZ.Zero).Then(FzOutputHeadRotX.NoRot),
                If(FzInputBallVelZ.Positive).Then(FzOutputHeadRotX.RotNegDir),
            };

            return rules;

        }


        private FuzzyRule<FzOutputHeadRotZ>[] GetHeadRotZRules()
        {
            FuzzyRule<FzOutputHeadRotZ>[] rules = new FuzzyRule<FzOutputHeadRotZ>[]
            {
                If(FzInputBallPosX.Positive).Then(FzOutputHeadRotZ.RotPosDir),
                If(FzInputBallPosX.Zero).Then(FzOutputHeadRotZ.NoRot),
                If(FzInputBallPosX.Negative).Then(FzOutputHeadRotZ.RotNegDir),

                If(FzInputBallVelX.Positive).Then(FzOutputHeadRotZ.RotPosDir),
                If(FzInputBallVelX.Zero).Then(FzOutputHeadRotZ.NoRot),
                If(FzInputBallVelX.Negative).Then(FzOutputHeadRotZ.RotNegDir),
            };

            return rules;

        }

        private FuzzyRuleSet<FzOutputHeadRotX> GetHeadRotXRuleSet(FuzzySet<FzOutputHeadRotX> headRotX)
        {
            var rules = this.GetHeadRotXRules();
            return new FuzzyRuleSet<FzOutputHeadRotX>(headRotX, rules);
        }

        private FuzzyRuleSet<FzOutputHeadRotZ> GetHeadRotZRuleSet(FuzzySet<FzOutputHeadRotZ> headRotZ)
        {
            var rules = this.GetHeadRotZRules();
            return new FuzzyRuleSet<FzOutputHeadRotZ>(headRotZ, rules);
        }


        void ResetBall()
        {
            const float maxSpeed = 1f;
            m_BallRb.velocity = new Vector3(Random.Range(-maxSpeed, maxSpeed), 0f, Random.Range(-maxSpeed, maxSpeed));

            ball.transform.position = Vector3.up * 4.8f;

            const float absOffs = 1.2f;

            ball.transform.position = new Vector3(Random.Range(-absOffs, absOffs), ball.transform.position.y, Random.Range(-absOffs, absOffs)) + gameObject.transform.position;

            // Optionally isolate x or z random positioning:

            // ball.transform.position = new Vector3(Random.Range(-1.5f, 1.5f), 4f, 0f) + gameObject.transform.position;

            // ball.transform.position = new Vector3(0f, 4f, Random.Range(-1.5f, 1.5f)) + gameObject.transform.position;
        }


        void Start()
        {

            topCenterOfHead = new GameObject("TopCenterOfHead");
            topCenterOfHead.transform.SetPositionAndRotation(this.transform.position + Vector3.up * halfWidth, Quaternion.identity);
            topCenterOfHead.transform.SetParent(this.transform);

            // Some randomized start condition
            gameObject.transform.rotation = new Quaternion(0f, 0f, 0f, 0f);
            const float maxAbsDegrees = 10f;
            gameObject.transform.Rotate(new Vector3(1, 0, 0), Random.Range(-maxAbsDegrees, maxAbsDegrees));
            gameObject.transform.Rotate(new Vector3(0, 0, 1), Random.Range(-maxAbsDegrees, maxAbsDegrees));

            ResetBall();

            // Fuzzy init
            inputBallPosXSet = GetBallPosXSet();
            inputBallPosZSet = GetBallPosZSet();

            inputBallVelXSet = GetBallVelXSet();
            inputBallVelZSet = GetBallVelZSet();

            outputHeadRotXSet = GetHeadRotXSet();
            outputHeadRotZSet = GetHeadRotZSet();

            headRotXRuleSet = GetHeadRotXRuleSet(outputHeadRotXSet);
            headRotZRuleSet = GetHeadRotZRuleSet(outputHeadRotZSet);

        }


        public void ApplyFuzzyRules<T, S>(
            FuzzyRuleSet<T> headRotXRuleSet,
            FuzzyRuleSet<S> headRotZRuleSet,
            FuzzyValueSet fuzzyValueSet,
            out FuzzyValue<T>[] headRotXRuleOutput,
            out FuzzyValue<S>[] headRotZRuleOutput,
            ref FuzzyValueSet mergedHeadRotX,
            ref FuzzyValueSet mergedHeadRotZ,
            out float crispHeadRotXVal,
            out float crispHeadRotZVal
            )
            where T : struct, IConvertible where S : struct, IConvertible
        {
            // Perform rule evaluation one step at a time so we can extract debugging information

            headRotXRuleOutput = headRotXRuleSet.RuleEvaluator.EvaluateRules(headRotXRuleSet.Rules, fuzzyValueSet);
            var headRotXMerger = headRotXRuleSet.OutputsMerger;
            headRotXMerger.MergeValues(headRotXRuleOutput, mergedHeadRotX);
            var headRotXDefuzz = headRotXRuleSet.Defuzzer;
            crispHeadRotXVal = headRotXDefuzz.Defuzze(headRotXRuleSet.OutputVarSet, mergedHeadRotX);

            headRotZRuleOutput = headRotZRuleSet.RuleEvaluator.EvaluateRules(headRotZRuleSet.Rules, fuzzyValueSet);
            var headRotZMerger = headRotZRuleSet.OutputsMerger;
            headRotZMerger.MergeValues(headRotZRuleOutput, mergedHeadRotZ);
            var headRotZDefuzz = headRotZRuleSet.Defuzzer;
            crispHeadRotZVal = headRotZDefuzz.Defuzze(headRotZRuleSet.OutputVarSet, mergedHeadRotZ);

            var newAngle = new Vector3(crispHeadRotXVal, 0f, crispHeadRotZVal);

            // Only write a new value if it is different so that rigidbody can go to sleep
            if (!gameObject.transform.eulerAngles.Equals(newAngle))
            {
                // Using smoothDamp to simulate some inertia in control authority
                gameObject.transform.rotation = QuaternionUtil.SmoothDamp(transform.rotation, Quaternion.Euler(newAngle), ref quaternionDeriv, RotationSmoothTime);
            }

        }

        // Some Debugging variables that will be visible in Inspector Window
        [SerializeField]
        float DEBUG_ballPosX;
        [SerializeField]
        float DEBUG_ballPosZ;

        [SerializeField]
        float DEBUG_ballVelX;
        [SerializeField]
        float DEBUG_ballVelZ;

        [SerializeField]
        float DEBUG_HeadRotX;
        [SerializeField]
        float DEBUG_HeadRotZ;

        [SerializeField]
        bool DEBUG_ballSleeping = false;

        [SerializeField]
        int DEBUG_ballDropCount = 0;


        private void Update()
        {
            // did ball stop moving (e.g. fully balanced in middle)?
            if (m_BallRb.IsSleeping())
            {
                DEBUG_ballSleeping = true;

                ResetBall();

            }
            else
            {
                DEBUG_ballSleeping = false;
            }

            if (ball.transform.position.y < -10f)
            {
                Debug.Log("BALL DROPPED!");

                ++DEBUG_ballDropCount;

                ResetBall();
            }

            // relative to top-center position of balance guy's head

            var currBallPosX = ball.transform.position.x - topCenterOfHead.transform.position.x;
            var currBallPosZ = ball.transform.position.z - topCenterOfHead.transform.position.z;

            //var currBallPosX = ball.transform.position.x - gameObject.transform.position.x;
            //var currBallPosZ = ball.transform.position.z - gameObject.transform.position.z;

            // Fuzzification
            inputBallPosXSet.Evaluate(currBallPosX, fzInputValueSet);
            inputBallPosZSet.Evaluate(currBallPosZ, fzInputValueSet);

            inputBallVelXSet.Evaluate(m_BallRb.velocity.x, fzInputValueSet);
            inputBallVelZSet.Evaluate(m_BallRb.velocity.z, fzInputValueSet);

            // Fuzzy Rules evaluation and Defuzzification
            ApplyFuzzyRules<FzOutputHeadRotX, FzOutputHeadRotZ>(
                headRotXRuleSet,
                headRotZRuleSet,
                fzInputValueSet,
                out var throttleRuleOutput,
                out var wheelRuleOutput,
                ref mergedHeadRotX,
                ref mergedHeadRotZ,
                out var crispHeadRotXVal,
                out var crispHeadRotZVal
                );

            // Some debugging info visible in Inspector Window
            DEBUG_ballPosX = currBallPosX;
            DEBUG_ballPosZ = currBallPosZ;

            DEBUG_ballVelX = m_BallRb.velocity.x;
            DEBUG_ballVelZ = m_BallRb.velocity.z;

            DEBUG_HeadRotX = crispHeadRotXVal;
            DEBUG_HeadRotZ = crispHeadRotZVal;

        }


    }

}