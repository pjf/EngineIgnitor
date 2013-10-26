﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineIgnitor
{
	public class UllageSimulator
	{
		public static bool s_SimulateUllage = true;
		public static bool s_ShutdownEngineWhenUnstable = true;
		public static bool s_ExplodeEngineWhenTooUnstable = false;

		float ullageHeightMin, ullageHeightMax;
		float ullageRadialMin, ullageRadialMax;

		string fuelFlowState = "";

		public void Reset()
		{
			ullageHeightMax = 0.05f; ullageHeightMax = 0.95f;
			ullageRadialMin = 0.0f; ullageRadialMax = 0.95f;
		}

		public void Update(Vessel vessel, Part engine, float deltaTime)
		{
			if (vessel.isActiveVessel == false) return;

			Vector3 localAcceleration = new Vector3(0.0f, 0.0f, 0.0f);
			localAcceleration = engine.transform.InverseTransformDirection(vessel.acceleration - FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()));
			Vector3 localAccelerationAmount = localAcceleration * deltaTime;
			Vector3 rotation = new Vector3();
			Vector3 rotationAmount = new Vector3();
			if (engine.rigidbody != null && engine.rigidbody.angularVelocity != null)
			{
				rotation = engine.transform.InverseTransformDirection(engine.rigidbody.angularVelocity);
				rotationAmount = rotation * deltaTime;
			}

			if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)
			{ 
				// Time warping... (5x -> 100000x)
				//Debug.Log("Vessel state: " + vessel.Landed.ToString() + " " + vessel.Splashed.ToString() + " " + vessel.LandedOrSplashed.ToString());
				if (vessel.LandedOrSplashed == true)
				{
					localAcceleration = engine.transform.InverseTransformDirection( -FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()));
					localAccelerationAmount = localAcceleration * deltaTime;
					rotation.Set(0.0f, 0.0f, 0.0f);
					rotationAmount.Set(0.0f, 0.0f, 0.0f);
				}
				else
				{
					localAcceleration.Set(0.0f, 0.0f, 0.0f);
					localAccelerationAmount.Set(0.0f, 0.0f, 0.0f);
					rotation.Set(0.0f, 0.0f, 0.0f);
					rotationAmount.Set(0.0f, 0.0f, 0.0f);
				}
			}

			//Debug.Log("Ullage: dt: " + deltaTime.ToString("F2") + " localAcc: " + localAcceleration.ToString() + " rotateRate: " + rotation.ToString());
			
			// Natural diffusion.
			ullageHeightMin = Mathf.Lerp(ullageHeightMin, 0.05f, 0.01f * deltaTime);
			ullageHeightMax = Mathf.Lerp(ullageHeightMax, 0.95f, 0.01f * deltaTime);
			ullageRadialMin = Mathf.Lerp(ullageRadialMin, 0.00f, 0.02f * deltaTime);
			ullageRadialMax = Mathf.Lerp(ullageRadialMax, 0.95f, 0.02f * deltaTime);

			// Translate forward/backward.
			ullageHeightMin = Mathf.Clamp(ullageHeightMin + localAccelerationAmount.y * 0.06f, 0.0f, 0.9f);
			ullageHeightMax = Mathf.Clamp(ullageHeightMax + localAccelerationAmount.y * 0.06f, 0.1f, 1.0f);
			ullageRadialMin = Mathf.Clamp(ullageRadialMin - Mathf.Abs(localAccelerationAmount.y) * 0.06f, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax + Mathf.Abs(localAccelerationAmount.y) * 0.06f, 0.1f, 1.0f);

			// Translate up/down/left/right.
			Vector3 sideAcc = new Vector3(localAccelerationAmount.x, 0.0f, localAccelerationAmount.z);
			ullageHeightMin = Mathf.Clamp(ullageHeightMin - sideAcc.magnitude * 0.02f, 0.0f, 0.9f);
			ullageHeightMax = Mathf.Clamp(ullageHeightMax + sideAcc.magnitude * 0.02f, 0.1f, 1.0f);
			ullageRadialMin = Mathf.Clamp(ullageRadialMin + sideAcc.magnitude * 0.04f, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax + sideAcc.magnitude * 0.04f, 0.1f, 1.0f);

			// Rotate yaw/pitch.
			Vector3 rotateYawPitch = new Vector3(rotation.x, 0.0f, rotation.z);
			if(ullageHeightMin < 0.45)
				ullageHeightMin = Mathf.Clamp(ullageHeightMin + rotateYawPitch.magnitude * 0.004f, 0.0f, 0.45f);
			else
				ullageHeightMin = Mathf.Clamp(ullageHeightMin - rotateYawPitch.magnitude * 0.004f, 0.45f, 0.9f);

			if (ullageHeightMax < 0.55)
				ullageHeightMax = Mathf.Clamp(ullageHeightMax + rotateYawPitch.magnitude * 0.004f, 0.1f, 0.55f);
			else
				ullageHeightMax = Mathf.Clamp(ullageHeightMax - rotateYawPitch.magnitude * 0.004f, 0.55f, 1.0f);

			ullageRadialMin = Mathf.Clamp(ullageRadialMin - rotateYawPitch.magnitude * 0.002f, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax + rotateYawPitch.magnitude * 0.004f, 0.1f, 1.0f);
			
			// Rotate roll.
			ullageHeightMin = Mathf.Clamp(ullageHeightMin - Mathf.Abs(rotation.y) * 0.006f, 0.0f, 0.9f);
			ullageHeightMax = Mathf.Clamp(ullageHeightMax + Mathf.Abs(rotation.y) * 0.006f, 0.1f, 1.0f);
			ullageRadialMin = Mathf.Clamp(ullageRadialMin - Mathf.Abs(rotation.y) * 0.006f, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax - Mathf.Abs(rotation.y) * 0.003f, 0.1f, 1.0f);

			//Debug.Log("Ullage: Height: (" + ullageHeightMin.ToString("F2") + " - " + ullageHeightMax.ToString("F2") + ") Radius: (" + ullageRadialMin.ToString("F2") + " - " + ullageRadialMax.ToString("F2") + ")");
		}

		public float GetFuelFlowStability()
		{
			float bLevel = Mathf.Clamp((ullageHeightMax - ullageHeightMin) * (ullageRadialMax - ullageRadialMin) / 0.1f - 1.0f, 0.0f, 10.0f);
			//Debug.Log("Ullage: bLevel: " + bLevel.ToString("F3"));
	
			float pVertical = 1.0f;
			pVertical = 1.0f - (ullageHeightMin - 0.1f) / 0.2f;
			pVertical = Mathf.Clamp01(pVertical);
			//Debug.Log("Ullage: pVertical: " + pVertical.ToString("F3"));
	
			float pHorizontal = 1.0f;
			pHorizontal = 1.0f - (ullageRadialMin - 0.1f) / 0.2f;
			pHorizontal = Mathf.Clamp01(pHorizontal);
			//Debug.Log("Ullage: pHorizontal: " + pHorizontal.ToString("F3"));

			float successProbability = Mathf.Max(0.0f, 1.0f - (pVertical * pHorizontal * (0.75f + bLevel)));

			if (successProbability >= 0.996f)
				fuelFlowState = "Very Stable";
			else if (successProbability >= 0.95f)
				fuelFlowState = "Stable";
			else if (successProbability >= 0.75f)
				fuelFlowState = "Risky";
			else if (successProbability >= 0.50f)
				fuelFlowState = "Very Risky";
			else if (successProbability >= 0.30f)
				fuelFlowState = "Unstable";
			else
				fuelFlowState = "Very Unstable";

			return successProbability; 
		}

		public string GetFuelFlowState()
		{
			return fuelFlowState;
		}

	}
}