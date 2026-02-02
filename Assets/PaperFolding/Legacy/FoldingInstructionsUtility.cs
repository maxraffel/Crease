using UnityEngine;
using System.Collections.Generic;

namespace PaperFolding
{
    /// <summary>
    /// Utility methods for working with folding instructions
    /// </summary>
    public static class FoldingInstructionsUtility
    {
        /// <summary>
        /// Create a simple valley fold instruction sequence
        /// </summary>
        public static FoldingInstructions CreateValleyFold(string name, Vector2 handleUV, float angle = 180f)
        {
            var instructions = ScriptableObject.CreateInstance<FoldingInstructions>();
            instructions.sequenceName = name;
            instructions.description = "Simple valley fold";
            
            instructions.AddFoldStep(new FoldStepData
            {
                handleUV = handleUV,
                tagName = "valley",
                tagExpression = "",
                foldAngle = angle,
                duration = 0f
            });
            
            return instructions;
        }

        /// <summary>
        /// Create a symmetric fold (e.g., for wings)
        /// </summary>
        public static FoldingInstructions CreateSymmetricFold(string name, 
            Vector2 leftHandleUV, Vector2 rightHandleUV, float angle = 90f)
        {
            var instructions = ScriptableObject.CreateInstance<FoldingInstructions>();
            instructions.sequenceName = name;
            instructions.description = "Symmetric fold pattern";
            
            // Left fold
            instructions.AddFoldStep(new FoldStepData
            {
                handleUV = leftHandleUV,
                tagName = "left_fold",
                tagExpression = "",
                foldAngle = angle,
                duration = 0f
            });
            
            // Right fold
            instructions.AddFoldStep(new FoldStepData
            {
                handleUV = rightHandleUV,
                tagName = "right_fold",
                tagExpression = "NOT left_fold",
                foldAngle = angle,
                duration = 0f
            });
            
            return instructions;
        }

        /// <summary>
        /// Add a cinematic camera orbit to existing instructions
        /// </summary>
        public static void AddCameraOrbit(FoldingInstructions instructions, 
            float radius = 10f, int numSteps = 8, float stepDuration = 0.5f)
        {
            if (instructions == null) return;

            for (int i = 0; i < numSteps; i++)
            {
                float angle = (360f / numSteps) * i;
                
                instructions.AddCameraMoveStep(new CameraMoveStep
                {
                    rotation = new Vector3(30f, angle, 0f),
                    distance = radius,
                    duration = stepDuration,
                    easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
                });
            }
        }

        /// <summary>
        /// Validate and get detailed error report for instructions
        /// </summary>
        public static string GetValidationReport(FoldingInstructions instructions)
        {
            if (instructions == null)
                return "Instructions is null";

            var report = new System.Text.StringBuilder();
            report.AppendLine($"Validation Report for: {instructions.sequenceName}");
            report.AppendLine($"Total Steps: {instructions.steps.Count}");
            report.AppendLine();

            // Check expression syntax
            var errors = instructions.ValidateAllExpressions();
            if (errors.Count > 0)
            {
                report.AppendLine("Expression Errors:");
                foreach (var (stepIndex, errorMessage) in errors)
                {
                    report.AppendLine($"  Step {stepIndex}: {errorMessage}");
                }
                report.AppendLine();
            }
            else
            {
                report.AppendLine("✓ All expressions valid");
                report.AppendLine();
            }

            // Check undefined tags
            bool hasUndefinedTags = false;
            for (int i = 0; i < instructions.steps.Count; i++)
            {
                var undefinedTags = instructions.GetUndefinedTagsAtStep(i);
                if (undefinedTags.Count > 0)
                {
                    if (!hasUndefinedTags)
                    {
                        report.AppendLine("Undefined Tag References:");
                        hasUndefinedTags = true;
                    }
                    report.AppendLine($"  Step {i}: {string.Join(", ", undefinedTags)}");
                }
            }

            if (!hasUndefinedTags)
            {
                report.AppendLine("✓ All tag references valid");
            }

            return report.ToString();
        }

        /// <summary>
        /// Clone an existing instruction sequence
        /// </summary>
        public static FoldingInstructions Clone(FoldingInstructions source)
        {
            if (source == null) return null;

            var clone = ScriptableObject.CreateInstance<FoldingInstructions>();
            clone.sequenceName = source.sequenceName + " (Copy)";
            clone.description = source.description;
            clone.autoPlay = source.autoPlay;
            clone.loop = source.loop;

            // Deep copy steps
            foreach (var step in source.steps)
            {
                if (step is FoldStepData foldStep)
                {
                    clone.AddFoldStep(new FoldStepData
                    {
                        handleUV = foldStep.handleUV,
                        tagName = foldStep.tagName,
                        tagExpression = foldStep.tagExpression,
                        foldAngle = foldStep.foldAngle,
                        duration = foldStep.duration
                    });
                }
                else if (step is CameraMoveStep cameraStep)
                {
                    clone.AddCameraMoveStep(new CameraMoveStep
                    {
                        rotation = cameraStep.rotation,
                        distance = cameraStep.distance,
                        duration = cameraStep.duration,
                        easeCurve = new AnimationCurve(cameraStep.easeCurve.keys)
                    });
                }
            }

            return clone;
        }

        /// <summary>
        /// Merge multiple instruction sequences into one
        /// </summary>
        public static FoldingInstructions Merge(string name, params FoldingInstructions[] sequences)
        {
            var merged = ScriptableObject.CreateInstance<FoldingInstructions>();
            merged.sequenceName = name;
            merged.description = $"Merged sequence from {sequences.Length} sources";

            var tagMapping = new Dictionary<string, string>(); // oldTag -> newTag
            int tagCounter = 0;

            foreach (var sequence in sequences)
            {
                if (sequence == null) continue;

                tagMapping.Clear();

                // Process each step
                foreach (var step in sequence.steps)
                {
                    if (step is FoldStepData foldStep)
                    {
                        // Rename tags to avoid conflicts
                        string newTagName = foldStep.tagName;
                        if (!string.IsNullOrEmpty(newTagName))
                        {
                            newTagName = $"merged_tag_{tagCounter++}";
                            tagMapping[foldStep.tagName] = newTagName;
                        }

                        // Remap expression tags
                        string newExpression = foldStep.tagExpression;
                        foreach (var kvp in tagMapping)
                        {
                            newExpression = newExpression.Replace(kvp.Key, kvp.Value);
                        }

                        merged.AddFoldStep(new FoldStepData
                        {
                            handleUV = foldStep.handleUV,
                            tagName = newTagName,
                            tagExpression = newExpression,
                            foldAngle = foldStep.foldAngle,
                            duration = foldStep.duration
                        });
                    }
                    else if (step is CameraMoveStep cameraStep)
                    {
                        merged.AddCameraMoveStep(new CameraMoveStep
                        {
                            rotation = cameraStep.rotation,
                            distance = cameraStep.distance,
                            duration = cameraStep.duration,
                            easeCurve = new AnimationCurve(cameraStep.easeCurve.keys)
                        });
                    }
                }
            }

            return merged;
        }

        /// <summary>
        /// Extract a subsequence from an instruction set
        /// </summary>
        public static FoldingInstructions ExtractSubsequence(FoldingInstructions source, 
            int startIndex, int endIndex, string name)
        {
            if (source == null) return null;
            if (startIndex < 0 || endIndex >= source.steps.Count || startIndex > endIndex)
                return null;

            var subsequence = ScriptableObject.CreateInstance<FoldingInstructions>();
            subsequence.sequenceName = name;
            subsequence.description = $"Subsequence from {source.sequenceName} (steps {startIndex}-{endIndex})";

            for (int i = startIndex; i <= endIndex; i++)
            {
                var step = source.steps[i];
                
                if (step is FoldStepData foldStep)
                {
                    subsequence.AddFoldStep(new FoldStepData
                    {
                        handleUV = foldStep.handleUV,
                        tagName = foldStep.tagName,
                        tagExpression = foldStep.tagExpression,
                        foldAngle = foldStep.foldAngle,
                        duration = foldStep.duration
                    });
                }
                else if (step is CameraMoveStep cameraStep)
                {
                    subsequence.AddCameraMoveStep(new CameraMoveStep
                    {
                        rotation = cameraStep.rotation,
                        distance = cameraStep.distance,
                        duration = cameraStep.duration,
                        easeCurve = new AnimationCurve(cameraStep.easeCurve.keys)
                    });
                }
            }

            return subsequence;
        }

        /// <summary>
        /// Generate common edge positions for handle placement
        /// </summary>
        public static class CommonHandles
        {
            // Bottom edge
            public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
            public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
            public static readonly Vector2 BottomRight = new Vector2(1f, 0f);

            // Top edge
            public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
            public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
            public static readonly Vector2 TopRight = new Vector2(1f, 1f);

            // Left edge
            public static readonly Vector2 LeftBottom = new Vector2(0f, 0f);
            public static readonly Vector2 LeftCenter = new Vector2(0f, 0.5f);
            public static readonly Vector2 LeftTop = new Vector2(0f, 1f);

            // Right edge
            public static readonly Vector2 RightBottom = new Vector2(1f, 0f);
            public static readonly Vector2 RightCenter = new Vector2(1f, 0.5f);
            public static readonly Vector2 RightTop = new Vector2(1f, 1f);
        }

        /// <summary>
        /// Generate common camera positions
        /// </summary>
        public static class CommonCameraPositions
        {
            public static readonly Vector3 Front = new Vector3(0f, 0f, 0f);
            public static readonly Vector3 Top = new Vector3(90f, 0f, 0f);
            public static readonly Vector3 Bottom = new Vector3(-90f, 0f, 0f);
            public static readonly Vector3 Left = new Vector3(0f, -90f, 0f);
            public static readonly Vector3 Right = new Vector3(0f, 90f, 0f);
            public static readonly Vector3 Isometric = new Vector3(30f, 45f, 0f);
        }
    }
}
