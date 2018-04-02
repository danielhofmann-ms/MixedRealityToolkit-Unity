﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace HoloToolkit.Unity.InputModule.Utilities.Interations
{
    /// <summary>
    /// Implements common logic for rotating holograms using a handlebar metaphor. 
    /// each frame, object_rotation_delta = rotation_delta(current_hands_vector, previous_hands_vector)
    /// where hands_vector is the vector between two hand/controller positions.
    /// 
    /// Usage:
    /// When a manipulation starts, call Setup.
    /// Call Update any time to update the move logic and get a new rotation for the object.
    /// </summary>
    public class TwoHandRotateLogic
    {
        /// <summary>
        /// Private variables
        /// </summary>
        private const float MinHandDistanceForPitchM = 0.1f;

        /// <summary>
        /// a scalar applied to Rotation angle generated by handlebar calculations.
        /// </summary>
        private const float RotationMultiplier = 2f;

        /// <summary>
        /// This enum value stores the initial constraint specified as an argument in the Constructor.
        /// It may be overridden by runtime conditions. The actual constraint is stored in the
        /// variable currentRotationConstraint.
        /// </summary>
        private AxisConstraint rotationConstraint;

        /// <summary>
        /// Vector storing last handlebar Rotation. The handlebar is the line imagined between the controllers/hands.
        /// </summary>
        private Vector3 previousHandlebarRotation;

        /// <summary>
        /// The current rotation constraint might be modified based on disambiguation logic, for example
        /// XOrYBasedOnInitialHandPosition might change the current rotation constraint based on the 
        /// initial hand positions at the start
        /// </summary>
        private AxisConstraint currentRotationConstraint;

        /// <summary>
        /// ProjectHandlebarGivenConstraint internal function to account for axis constraint
        /// </summary>
        /// <param name="constraint">Enum value describing the axis to which the rotation is constrained/param>
        /// <param name="handlebarRotation">A Vector3 describing the rotation of the line connecting the inputSources</param>
        /// <param name="manipulationRoot">Transform of gameObject to be two hand manipulated</param>
        /// <returns>a Vector3 describing handlebar after constraint is applied</returns>
        private Vector3 ProjectHandlebarGivenConstraint(AxisConstraint constraint, Vector3 handlebarRotation, Transform manipulationRoot)
        {
            Vector3 result = handlebarRotation;
            switch (constraint)
            {
                case AxisConstraint.XAxisOnly:
                    result.x = 0;
                    break;
                case AxisConstraint.YAxisOnly:
                    result.y = 0;
                    break;
                case AxisConstraint.ZAxisOnly:
                    result.z = 0;
                    break;
            }
            return CameraCache.Main.transform.TransformDirection(result);
        }

        /// <summary>
        /// GetHandlebarDirection internal function to get rotation described by inputSources.
        /// </summary>
        /// <param name="handsPressedMap">Dictionary listing inputSourceStates</param>
        /// <param name="manipulationRoot">Transform of gameObject to be two hand manipulated</param>
        /// <returns>A Vector3 describing the direction of the line connecting the inputSources</returns>
        private Vector3 GetHandlebarDirection(Dictionary<uint, Vector3> handsPressedMap, Transform manipulationRoot)
        {
            var handsEnumerator = handsPressedMap.Values.GetEnumerator();
            handsEnumerator.MoveNext();
            var hand1 = handsEnumerator.Current;
            handsEnumerator.MoveNext();
            var hand2 = handsEnumerator.Current;

            // We project the handlebar direction into camera space because otherwise when we move our body the handlebard will move even 
            // though, relative to our heads, the handlebar is not moving.
            hand1 = CameraCache.Main.transform.InverseTransformPoint(hand1);
            hand2 = CameraCache.Main.transform.InverseTransformPoint(hand2);

            return hand2 - hand1;
        }

        /// <summary>
        /// TwoHandRotateLogic Constructor
        /// </summary>
        /// <param name="constrainRotation">Enum describing to which axis the rotation is constrained</param>
        public TwoHandRotateLogic(AxisConstraint constrainRotation)
        {
            rotationConstraint = constrainRotation;
        }

        /// <summary>
        /// Initializes twohand system with controller/hand source info: 
        /// the Dictionary collection already filled with controller/hand info,
        /// and the Transform of the GameObject to be manipulated.
        /// </summary>
        /// <param name="handsPressedMap">Dictionary listing inputSourceStates</param>
        /// <param name="manipulationRoot">Transform of gameObject to be two hand manipulated</param>
        public void Setup(Dictionary<uint, Vector3> handsPressedMap, Transform manipulationRoot)
        {
            currentRotationConstraint = rotationConstraint;
            previousHandlebarRotation = GetHandlebarDirection(handsPressedMap, manipulationRoot);
        }

        /// <summary>
        /// Updates internal states based on current Controller/hand states.
        /// </summary>
        /// <param name="handsPressedMap">Dictionary listing inputSourceStates</param>
        /// <param name="manipulationRoot">Transform of gameObject to be two hand manipulated</param>
        /// <param name="currentRotation">New rotation to be applied</param>
        /// <returns>Quaternion describing rotation based on position of inputSources - Controllers/Hands</returns>
        public Quaternion Update(Dictionary<uint, Vector3> handsPressedMap, Transform manipulationRoot, Quaternion currentRotation)
        {
            var handlebarDirection = GetHandlebarDirection(handsPressedMap, manipulationRoot);
            var handlebarDirectionProjected = ProjectHandlebarGivenConstraint(currentRotationConstraint, handlebarDirection, manipulationRoot);
            var prevHandlebarDirectionProjected = ProjectHandlebarGivenConstraint(currentRotationConstraint, previousHandlebarRotation, manipulationRoot);
            previousHandlebarRotation = handlebarDirection;

            var rotationDelta = Quaternion.FromToRotation(prevHandlebarDirectionProjected, handlebarDirectionProjected);

            var angle = 0f;
            var axis = Vector3.zero;
            rotationDelta.ToAngleAxis(out angle, out axis);
            angle *= RotationMultiplier;

            if (currentRotationConstraint == AxisConstraint.YAxisOnly)
            {
                // If we are rotating about Y axis, then make sure we rotate about global Y axis.
                // Since the angle is obtained from a quaternion, we need to properly orient it (up or down) based
                // on the original axis-angle representation. 
                axis = Vector3.up * Vector3.Dot(axis, Vector3.up);
            }
            return Quaternion.AngleAxis(angle, axis) * currentRotation;
        }
    }
}
