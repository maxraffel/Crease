using UnityEngine;

namespace PaperFolding
{
    /// <summary>
    /// Base class for sequence steps in folding instructions
    /// </summary>
    [System.Serializable]
    public abstract class FoldStep
    {
        public abstract string GetStepType();
        public abstract string GetDisplayName();
    }

    /// <summary>
    /// A fold operation step
    /// </summary>
    [System.Serializable]
    public class FoldStepData : FoldStep
    {
        [Tooltip("UV coordinate for the drag handle (should be on edge)")]
        public Vector2 handleUV = new Vector2(0.5f, 0f);

        [Tooltip("Tag name to apply to vertices affected by this fold")]
        public string tagName = "fold_1";

        [Tooltip("Boolean expression of tags to filter which vertices can be folded (e.g., 'tag1 AND tag2' or 'tag1 OR NOT tag2')")]
        [TextArea(2, 5)]
        public string tagExpression = "";

        [Tooltip("Angle in degrees to fold")]
        public float foldAngle = 180f;

        [Tooltip("Duration for animated fold (0 = instant)")]
        public float duration = 0f;

        [Tooltip("Use camera view plane for drag instead of paper normal")]
        public bool useCameraPlane = false;

        [Header("Accuracy Tracking")]
        [Tooltip("Enable accuracy tracking for this fold")]
        public bool hasCorrectAxis = false;

        [Tooltip("Correct axis start point (UV coordinates)")]
        public Vector2 correctAxisStart = new Vector2(0f, 0.5f);

        [Tooltip("Correct axis end point (UV coordinates)")]
        public Vector2 correctAxisEnd = new Vector2(1f, 0.5f);

        [Tooltip("Flat score modifier applied after calculation (-100 to 100)")]
        [Range(-100f, 100f)]
        public float scoreModifier = 0f;

        /// <summary>
        /// Get the correct axis as a FoldAxis
        /// </summary>
        public FoldAxis GetCorrectAxis()
        {
            return new FoldAxis(correctAxisStart.x, correctAxisStart.y, correctAxisEnd.x, correctAxisEnd.y);
        }

        public override string GetStepType() => "Fold";
        
        public override string GetDisplayName()
        {
            string expr = string.IsNullOrEmpty(tagExpression) ? "all" : tagExpression;
            string planeMode = useCameraPlane ? "[CamPlane]" : "";
            string accuracy = hasCorrectAxis ? " [Scored]" : "";
            return $"Fold at ({handleUV.x:F2}, {handleUV.y:F2}) → tag '{tagName}' | filter: {expr} | angle: {foldAngle}° {planeMode}{accuracy}";
        }
    }

    /// <summary>
    /// A camera movement step
    /// </summary>
    [System.Serializable]
    public class CameraMoveStep : FoldStep
    {
        [Tooltip("Target camera rotation (Euler angles)")]
        public Vector3 rotation = Vector3.zero;

        [Tooltip("Target camera distance from the paper")]
        public float distance = 10f;

        [Tooltip("Duration of the camera movement in seconds")]
        public float duration = 1f;

        [Tooltip("Animation curve for camera movement")]
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public override string GetStepType() => "Camera";
        
        public override string GetDisplayName()
        {
            return $"Camera to rot({rotation.x:F0}, {rotation.y:F0}, {rotation.z:F0}) dist: {distance:F1} over {duration:F1}s";
        }
    }
}
