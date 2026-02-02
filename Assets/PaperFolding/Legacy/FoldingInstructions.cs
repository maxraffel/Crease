using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace PaperFolding
{
    /// <summary>
    /// ScriptableObject that stores a complete sequence of folding instructions and camera movements
    /// </summary>
    [CreateAssetMenu(fileName = "New Folding Instructions", menuName = "Paper Folding/Folding Instructions", order = 1)]
    public class FoldingInstructions : ScriptableObject
    {
        [Header("Metadata")]
        [Tooltip("Display name for this folding sequence")]
        public string sequenceName = "Untitled Sequence";

        [TextArea(2, 4)]
        [Tooltip("Description of what this folding sequence creates")]
        public string description = "";

        [Header("Steps")]
        [Tooltip("Sequence of fold and camera movement steps")]
        [SerializeReference]
        public List<FoldStep> steps = new List<FoldStep>();

        [Header("Playback Settings")]
        [Tooltip("Auto-play this sequence when loaded")]
        public bool autoPlay = false;

        [Tooltip("Loop the sequence when complete")]
        public bool loop = false;

        /// <summary>
        /// Add a fold step to the sequence
        /// </summary>
        public void AddFoldStep(FoldStepData foldStep)
        {
            steps.Add(foldStep);
        }

        /// <summary>
        /// Add a camera move step to the sequence
        /// </summary>
        public void AddCameraMoveStep(CameraMoveStep cameraStep)
        {
            steps.Add(cameraStep);
        }

        /// <summary>
        /// Remove a step at the specified index
        /// </summary>
        public void RemoveStep(int index)
        {
            if (index >= 0 && index < steps.Count)
            {
                steps.RemoveAt(index);
            }
        }

        /// <summary>
        /// Move a step to a new position in the sequence
        /// </summary>
        public void MoveStep(int fromIndex, int toIndex)
        {
            if (fromIndex >= 0 && fromIndex < steps.Count && 
                toIndex >= 0 && toIndex < steps.Count && 
                fromIndex != toIndex)
            {
                var step = steps[fromIndex];
                steps.RemoveAt(fromIndex);
                steps.Insert(toIndex, step);
            }
        }

        /// <summary>
        /// Get all tags that would be created by executing steps up to the given index
        /// </summary>
        public HashSet<string> GetTagsUpToStep(int stepIndex)
        {
            HashSet<string> tags = new HashSet<string>();

            for (int i = 0; i <= stepIndex && i < steps.Count; i++)
            {
                if (steps[i] is FoldStepData foldStep)
                {
                    if (!string.IsNullOrEmpty(foldStep.tagName))
                    {
                        // Each fold creates both _moved and _static variants
                        tags.Add(foldStep.tagName + "_moved");
                        tags.Add(foldStep.tagName + "_static");
                    }
                }
            }

            return tags;
        }

        /// <summary>
        /// Get all tags created by this entire sequence
        /// </summary>
        public HashSet<string> GetAllTags()
        {
            return GetTagsUpToStep(steps.Count - 1);
        }

        /// <summary>
        /// Get all tags referenced in expressions throughout the sequence
        /// </summary>
        public HashSet<string> GetAllReferencedTags()
        {
            HashSet<string> tags = new HashSet<string>();

            foreach (var step in steps)
            {
                if (step is FoldStepData foldStep)
                {
                    if (!string.IsNullOrEmpty(foldStep.tagExpression))
                    {
                        var referencedTags = BooleanExpressionEvaluator.ExtractTagNames(foldStep.tagExpression);
                        tags.UnionWith(referencedTags);
                    }
                }
            }

            return tags;
        }

        /// <summary>
        /// Validate all expressions in the sequence
        /// </summary>
        public List<(int stepIndex, string errorMessage)> ValidateAllExpressions()
        {
            List<(int, string)> errors = new List<(int, string)>();

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is FoldStepData foldStep)
                {
                    if (!string.IsNullOrEmpty(foldStep.tagExpression))
                    {
                        var (isValid, errorMessage) = BooleanExpressionEvaluator.ValidateExpression(foldStep.tagExpression);
                        if (!isValid)
                        {
                            errors.Add((i, errorMessage));
                        }
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Check if a step's tag expression references tags that don't exist yet
        /// </summary>
        public List<string> GetUndefinedTagsAtStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= steps.Count)
                return new List<string>();

            if (!(steps[stepIndex] is FoldStepData foldStep))
                return new List<string>();

            var referencedTags = BooleanExpressionEvaluator.ExtractTagNames(foldStep.tagExpression);
            var availableTags = GetTagsUpToStep(stepIndex - 1);

            return referencedTags.Where(tag => !availableTags.Contains(tag)).ToList();
        }

        /// <summary>
        /// Get a summary of the sequence for display
        /// </summary>
        public string GetSummary()
        {
            int foldCount = steps.Count(s => s is FoldStepData);
            int cameraCount = steps.Count(s => s is CameraMoveStep);
            
            return $"{sequenceName}\n" +
                   $"Steps: {steps.Count} ({foldCount} folds, {cameraCount} camera moves)\n" +
                   $"Tags: {GetAllTags().Count} created, {GetAllReferencedTags().Count} referenced";
        }
    }
}
