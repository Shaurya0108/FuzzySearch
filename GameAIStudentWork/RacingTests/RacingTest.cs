
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

using GameAI;

namespace Tests
{

    public class RacingTest
    {

        const int timeScale = 1; // how fast to run the game relative to frame rate. Running fast doesn't necessarily
                                 // give accurate results.

        const int PlayMatchTimeOutMS = int.MaxValue; // don't mess with this; add it to new tests
                                                     // as [Timeout(PlayMatchTimeOutMS)] (see below for
                                                     // example) It stops early default timeout

        public RacingTest()
        {
           
        }


        [UnityTest]
        [Timeout(PlayMatchTimeOutMS)]
        public IEnumerator TestFuzzyRace_Curvy()
        {
            return _TestFuzzyRace("RaceTrackFZ", 5f * 60f,
                            minAllowedSpeed: 30f,
                            targetSpeed: 45f,
                            extraCreditSpeed: 58f,
                            speedScoreWeight: 0.6f,
                            wipeoutScoreWeight: 0.4f,
                            extraCreditWeight: 0.05f,
                            gradeWeight: 0.30f
                ); ;
        }

        [UnityTest]
        [Timeout(PlayMatchTimeOutMS)]
        public IEnumerator TestFuzzyRace_Winding()
        {
            return _TestFuzzyRace("WindingRaceTrackFZ", 5f * 60f,
                            minAllowedSpeed: 30f,
                            targetSpeed: 45f,
                            extraCreditSpeed: 58f,
                            speedScoreWeight: 0.6f,
                            wipeoutScoreWeight: 0.4f,
                            extraCreditWeight: 0.05f,
                            gradeWeight: 0.30f
                );
        }

        [UnityTest]
        [Timeout(PlayMatchTimeOutMS)]
        public IEnumerator TestFuzzyRace_DragRace()
        {
            return _TestFuzzyRace("DragRaceTrackFZ", 60f,
                            minAllowedSpeed: 60f,
                            targetSpeed: 90f,
                            extraCreditSpeed: 120f,
                            speedScoreWeight: 0.8f,
                            wipeoutScoreWeight: 0.2f,
                            extraCreditWeight: 0.05f,
                            gradeWeight: 0.10f
                );
        }

        [UnityTest]
        [Timeout(PlayMatchTimeOutMS)]
        public IEnumerator TestFuzzyRace_FastSweepers()
        {
            return _TestFuzzyRace("FastSweepersRaceTrackFZ", 5f * 60f,
                            minAllowedSpeed: 30f,
                            targetSpeed: 70f,
                            extraCreditSpeed: 85f,
                            speedScoreWeight: 0.6f,
                            wipeoutScoreWeight: 0.4f,
                            extraCreditWeight: 0.05f,
                            gradeWeight: 0.30f
                );
        }

        [Timeout(PlayMatchTimeOutMS)]
        public IEnumerator _TestFuzzyRace(string sceneName, float duration_s,
            float minAllowedSpeed, float targetSpeed, float extraCreditSpeed,
            float speedScoreWeight, float wipeoutScoreWeight, float extraCreditWeight, float gradeWeight)
        {
            Time.timeScale = timeScale;

            GameManager.INTERNAL_overrideSimulationMode = GameManager.SimulationMode.FPS_60_1X_SimTime;

            //var sceneName = "RaceTrackFZ";

            SceneManager.LoadScene(sceneName);

            var waitForScene = new WaitForSceneLoaded(sceneName);
            yield return waitForScene;

            Assert.IsFalse(waitForScene.TimedOut, "Scene " + sceneName + " was never loaded");

            yield return new WaitForSeconds(duration_s);

            var gm = GameManager.Instance;

            Debug.Log($"Km/H LTA: {gm.KpHLTA} Num wipeouts: {gm.Wipeouts} meters: {gm.MetersTravelled}");


            var speedPenalty = Mathf.Lerp(speedScoreWeight, 0f,
  
                Power(
                    Mathf.InverseLerp(minAllowedSpeed, targetSpeed, gm.KpHLTA)
                    , 0.5f)
 
                    );


            var maxAllowedWipeouts = 1;
            var maxPartialPenaltyWipeouts = 10;

 
            var wipeoutPenalty = Mathf.Lerp(wipeoutScoreWeight, 0f,
                    1f - Mathf.InverseLerp(maxAllowedWipeouts, maxPartialPenaltyWipeouts, gm.Wipeouts)
                );


            float extraCredit = 0f;

            if(gm.Wipeouts <= 0)
            {
                extraCredit = Mathf.Lerp(0f, extraCreditWeight,

                     Power(
                         Mathf.InverseLerp(targetSpeed, extraCreditSpeed, gm.KpHLTA)
                         , 0.5f)

                         );

                Debug.Log($"Extra credit earned: {extraCredit}");
            }
            else
            {
                Debug.Log($"Extra credit only earned if no wipeouts!");
            }

            var totalScore = (speedScoreWeight - speedPenalty) + 
                (wipeoutScoreWeight - wipeoutPenalty) + 
                extraCredit;

            Debug.Log($"Estimated Total Score: {totalScore*100}%");
            Debug.Log($"Estimated Weighted Grade Contribution (wt: {gradeWeight}): {gradeWeight * totalScore * 100}");

        }

        float Power(float t, float strength)
        {
            return Mathf.Pow(t, strength);
        }


    }

}

