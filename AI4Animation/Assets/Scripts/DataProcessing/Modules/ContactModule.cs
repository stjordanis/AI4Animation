﻿#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

public class ContactModule : DataModule {

	public ContactFunction[] Functions = new ContactFunction[0];
	public string[] Names = new string[0];

	public override TYPE Type() {
		return TYPE.Contact;
	}

	public override DataModule Initialise(MotionData data) {
		Data = data;
		Inspect = true;
		Names = new string[Data.Source.Bones.Length];
		for(int i=0; i<Data.Source.Bones.Length; i++) {
			Names[i] = Data.Source.Bones[i].Name;
		}
		return this;
	}

	public void AddContact(int sensor) {
		if(System.Array.Exists(Functions, x => x.Sensor == sensor)) {
			Debug.Log("Contact for bone " + sensor + " already exists.");
			return;
		}
		ArrayExtensions.Add(ref Functions, new ContactFunction(this, sensor));
	}

	public void RemoveContact(int sensor) {
		int index = System.Array.FindIndex(Functions, x => x.Sensor == sensor);
		if(index == -1) {
			Debug.Log("Contact for bone " + sensor + " does not exist");
		} else {
			ArrayExtensions.RemoveAt(ref Functions, index);
		}
	}

	public void Compute() {
		for(int i=0; i<Functions.Length; i++) {
			Functions[i].Compute();
		}
	}
	
	public override void Draw(MotionEditor editor) {
		Frame frame = Data.GetFrame(editor.GetState().Index);
		UltiDraw.Begin();
		for(int i=0; i<Functions.Length; i++) {
			Matrix4x4 matrix = Functions[i].GetPivotTransformation(frame, editor.ShowMirror);
			Vector3 position = matrix.GetPosition();
			Quaternion rotation = matrix.GetRotation();
			UltiDraw.DrawSphere(position, Quaternion.identity, 0.025f, UltiDraw.Cyan.Transparent(0.5f));
			UltiDraw.DrawArrow(position, position + 0.25f * (rotation * Functions[i].Normal.normalized), 0.8f, 0.02f, 0.1f, UltiDraw.Cyan.Transparent(0.5f));
			UltiDraw.DrawSphere(position, Quaternion.identity, Functions[i].Threshold, UltiDraw.Mustard.Transparent(0.5f));
		}
		UltiDraw.End();
	}

	protected override void DerivedInspector(MotionEditor editor) {
		EditorGUILayout.BeginHorizontal();
		if(Utility.GUIButton("Add Contact", UltiDraw.DarkGrey, UltiDraw.White)) {
			ArrayExtensions.Add(ref Functions, new ContactFunction(this));
		}
		if(Utility.GUIButton("Remove Contact", UltiDraw.DarkGrey, UltiDraw.White)) {
			ArrayExtensions.Shrink(ref Functions);
		}
		EditorGUILayout.EndHorizontal();
		if(Utility.GUIButton("Compute", UltiDraw.DarkGrey, UltiDraw.White)) {
			Compute();
		}
		for(int i=0; i<Functions.Length; i++) {
			Functions[i].Inspector(editor);
		}
	}

	[System.Serializable]
	public class ContactFunction {
		public ContactModule Module = null;
		public int Sensor = 0;
		public float Threshold = 0.1f;
		public Vector3 Offset = Vector3.zero;
		public Vector3 Normal = Vector3.down;
		public LayerMask Mask = -1;
		public bool[] RegularContacts = new bool[0];
		public bool[] InverseContacts = new bool[0];

		public ContactFunction(ContactModule module) {
			Module = module;
			Sensor = 0;
			RegularContacts = new bool[Module.Data.GetTotalFrames()];
			InverseContacts = new bool[Module.Data.GetTotalFrames()];
			Compute();
		}
		
		public ContactFunction(ContactModule module, int sensor) {
			Module = module;
			Sensor = sensor;
			RegularContacts = new bool[Module.Data.GetTotalFrames()];
			InverseContacts = new bool[Module.Data.GetTotalFrames()];
			Compute();
		}

		public void SetSensor(int index) {
			if(Sensor != index) {
				Sensor = index;
				Compute();
			}
		}

		public void SetThreshold(float value) {
			if(Threshold != value) {
				Threshold = value;
				Compute();
			}
		}

		public void SetOffset(Vector3 offset) {
			if(Offset != offset) {
				Offset = offset;
				Compute();
			}
		}

		public void SetNormal(Vector3 normal) {
			if(Normal != normal) {
				Normal = normal;
				Compute();
			}
		}

		public void SetMask(LayerMask mask) {
			if(Mask != mask) {
				Mask = mask;
				Compute();
			}
		}

		public bool HasContact(Frame frame, bool mirrored) {
			return mirrored ? InverseContacts[frame.Index-1] : RegularContacts[frame.Index-1];
		}

		public Matrix4x4 GetPivotTransformation(Frame frame, bool mirrored) {
			Matrix4x4 matrix = frame.GetBoneTransformation(Sensor, mirrored);
			Matrix4x4Extensions.SetPosition(ref matrix, matrix.GetPosition() + matrix.GetRotation() * Offset);
			return matrix;
		}

		public void Compute() {
			for(int i=0; i<Module.Data.GetTotalFrames(); i++) {
				Matrix4x4 rMatrix = GetPivotTransformation(Module.Data.Frames[i], false);
				RegularContacts[i] = Physics.Raycast(rMatrix.GetPosition() - Threshold * (rMatrix.GetRotation() * Normal), rMatrix.GetRotation() * Normal, 2f*Threshold, Mask.value);

				Matrix4x4 iMatrix = GetPivotTransformation(Module.Data.Frames[i], true);
				InverseContacts[i] = Physics.Raycast(iMatrix.GetPosition() - Threshold * (iMatrix.GetRotation() * Normal), iMatrix.GetRotation() * Normal, 2f*Threshold, Mask.value);
			}
		}

		public void Inspector(MotionEditor editor) {
			UltiDraw.Begin();

			UltiDraw.DrawSphere(Vector3.zero, Quaternion.identity, 1f, Color.red);

			Utility.SetGUIColor(UltiDraw.Grey);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();

				Frame frame = Module.Data.GetFrame(editor.GetState().Index);

				SetSensor(EditorGUILayout.Popup("Sensor", Sensor, Module.Names));
				SetThreshold(EditorGUILayout.FloatField("Threshold", Threshold));
				SetOffset(EditorGUILayout.Vector3Field("Offset", Offset));
				SetNormal(EditorGUILayout.Vector3Field("Normal", Normal));
				SetMask(InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(EditorGUILayout.MaskField("Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(Mask), InternalEditorUtility.layers)));

				EditorGUILayout.BeginVertical(GUILayout.Height(50f));
				Rect ctrl = EditorGUILayout.GetControlRect();
				Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, 50f);
				EditorGUI.DrawRect(rect, UltiDraw.Black);

				float startTime = frame.Timestamp-editor.GetWindow()/2f;
				float endTime = frame.Timestamp+editor.GetWindow()/2f;
				if(startTime < 0f) {
					endTime -= startTime;
					startTime = 0f;
				}
				if(endTime > Module.Data.GetTotalTime()) {
					startTime -= endTime-Module.Data.GetTotalTime();
					endTime = Module.Data.GetTotalTime();
				}
				startTime = Mathf.Max(0f, startTime);
				endTime = Mathf.Min(Module.Data.GetTotalTime(), endTime);
				int start = Module.Data.GetFrame(startTime).Index;
				int end = Module.Data.GetFrame(endTime).Index;
				int elements = end-start;

				Vector3 prevPos = Vector3.zero;
				Vector3 newPos = Vector3.zero;
				Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
				Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

				//Contacts
				for(int i=start; i<=end; i++) {
					top.x = rect.xMin + (float)(i-start)/elements * rect.width;
					bottom.x = rect.xMin + (float)(i-start)/elements * rect.width;

					top.y = rect.yMax - rect.height;
					bottom.y = rect.yMax - rect.height/2f;
					if(RegularContacts[i-1]) {
						UltiDraw.DrawLine(top, bottom, UltiDraw.Green);
					}

					top.y = rect.yMax - rect.height/2f;
					bottom.y = rect.yMax;
					if(InverseContacts[i-1]) {
						UltiDraw.DrawLine(top, bottom, UltiDraw.Green);
					}
				}

				//Sequences
				for(int i=0; i<Module.Data.Sequences.Length; i++) {
					float left = rect.x + (float)(Module.Data.Sequences[i].Start-1)/(float)(Module.Data.GetTotalFrames()-1) * rect.width;
					float right = rect.x + (float)(Module.Data.Sequences[i].End-1)/(float)(Module.Data.GetTotalFrames()-1) * rect.width;
					Vector3 a = new Vector3(left, rect.y, 0f);
					Vector3 b = new Vector3(right, rect.y, 0f);
					Vector3 c = new Vector3(left, rect.y+rect.height, 0f);
					Vector3 d = new Vector3(right, rect.y+rect.height, 0f);
					UltiDraw.DrawTriangle(a, c, b, UltiDraw.Yellow.Transparent(0.25f));
					UltiDraw.DrawTriangle(b, c, d, UltiDraw.Yellow.Transparent(0.25f));
				}

				//Current Pivot
				top.x = rect.xMin + (float)(frame.Index-start)/elements * rect.width;
				bottom.x = rect.xMin + (float)(frame.Index-start)/elements * rect.width;
				top.y = rect.yMax - rect.height;
				bottom.y = rect.yMax;
				UltiDraw.DrawLine(top, bottom, UltiDraw.Yellow);
				UltiDraw.DrawCircle(top, 3f, UltiDraw.Green);
				UltiDraw.DrawCircle(bottom, 3f, UltiDraw.Green);

				Handles.DrawLine(Vector3.zero, Vector3.zero); //Somehow needed to get it working...

				EditorGUILayout.EndVertical();

			}
			UltiDraw.End();
		}
	}

}
#endif
