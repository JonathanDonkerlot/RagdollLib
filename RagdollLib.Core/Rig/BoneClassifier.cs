using System;
using RagdollLib.Core.Model;

namespace RagdollLib.Core.Rig
{
    /// <summary>
    /// Classifies arbitrary skeletal bone transform names into canonical <see cref="BoneRole"/> values
    /// while filtering out micro-bones, facial joints, LOD meshes, and interactive helper nodes.
    /// </summary>
    public static class BoneClassifier
    {
        private static readonly string[] ExcludedTokens =
        {
            "eye", "lid", "brow", "pupil", "lash", "jaw", "tongue", "teeth", "tooth",
            "mouth", "lip", "cheek", "nose", "nostril", "ear", "feather", "hair",
            "toe", "thumb", "index", "middle", "ring", "little", "pinky", "finger",
            "claw", "hoof", "horn", "wingtip", "beak",
            "clavicle", "collar", "scapula", "twist", "roll",
            "wrist", "hand", "palm", "ankle", "foot", "heel", "ball",
            "end", "term", "nub", "tip", "dummy", "helper", "target", "pivot",
            "interactor", "collider", "heat", "dock", "lod", "impostor", "geo",
            "mesh", "camera", "light", "audio", "effect", "particle", "mov",
            "stat", "bar", "canvas", "text", "pointer", "grip", "trigger"
        };

        /// <summary>
        /// Normalizes a bone name into lowercase underscore-separated tokens for matching.
        /// </summary>
        public static string NormalizeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            string s = rawName.Trim();

            // Strip common rig prefixes
            string[] prefixes = { "mixamorig:", "bip01 ", "bip01_", "bip_", "def-", "org-", "mch-", "skin_", "jnt_", "bn_" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (s.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(prefixes[i].Length);
                    break;
                }
            }

            // Insert underscores around camelCase boundaries and normalize separators
            var chars = new char[s.Length * 2];
            int len = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ':' || c == '-' || c == '.' || c == ' ' || c == '/')
                {
                    if (len > 0 && chars[len - 1] != '_')
                        chars[len++] = '_';
                    continue;
                }

                if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1]))
                {
                    if (len > 0 && chars[len - 1] != '_')
                        chars[len++] = '_';
                }

                chars[len++] = char.ToLowerInvariant(c);
            }

            string normalized = new string(chars, 0, len).Trim('_');
            if (normalized.EndsWith("_jnt", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 4);
            else if (normalized.EndsWith("_bone", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 5);

            return normalized.Trim('_');
        }

        /// <summary>
        /// Returns true if the node should never be turned into a ragdoll physics bone.
        /// </summary>
        public static bool ShouldExclude(string rawName, bool isExcludedComponent)
        {
            if (isExcludedComponent || string.IsNullOrWhiteSpace(rawName))
                return true;

            string norm = NormalizeName(rawName);
            if (string.IsNullOrEmpty(norm))
                return true;

            // Check raw suffixes that indicate non-deforming terminal helpers
            if (rawName.EndsWith("_End", StringComparison.OrdinalIgnoreCase) ||
                rawName.EndsWith("_term", StringComparison.OrdinalIgnoreCase) ||
                rawName.EndsWith("_mov", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] tokens = norm.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = StripTrailingDigits(tokens[i]);
                for (int j = 0; j < ExcludedTokens.Length; j++)
                {
                    if (tok == ExcludedTokens[j] ||
                        (tok.StartsWith(ExcludedTokens[j], StringComparison.Ordinal) &&
                         (ExcludedTokens[j] == "toe" || ExcludedTokens[j] == "feather" ||
                          ExcludedTokens[j] == "tongue" || ExcludedTokens[j] == "lid" ||
                          ExcludedTokens[j] == "easter")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to classify a bone name into a canonical <see cref="BoneRole"/>.
        /// Returns <see cref="BoneRole.Unknown"/> if the name does not match a known major bone.
        /// </summary>
        public static BoneRole Classify(string rawName, bool isExcludedComponent = false)
        {
            if (ShouldExclude(rawName, isExcludedComponent))
                return BoneRole.Unknown;

            string norm = NormalizeName(rawName);
            string[] tokens = norm.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return BoneRole.Unknown;

            bool isLeft = false;
            bool isRight = false;
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                if (t == "l" || t == "left" || t == "lf")
                    isLeft = true;
                else if (t == "r" || t == "right" || t == "rt")
                    isRight = true;
            }

            // Detect side prefixes like "lthigh" or "rshoulder" when not underscore-separated
            string joined = norm.Replace("_", string.Empty);

            if (HasAnyToken(tokens, "pelvis", "hips", "hip", "root"))
                return BoneRole.Pelvis;

            if (HasAnyToken(tokens, "chest", "ribcage", "thorax", "upperbody") || joined.Contains("spine2") || joined.Contains("spine02"))
                return BoneRole.Chest;

            if (HasAnyToken(tokens, "spine", "back", "torso", "waist", "abdomen", "lumbar"))
                return BoneRole.Spine;

            if (HasAnyToken(tokens, "neck"))
                return BoneRole.Neck;

            if (HasAnyToken(tokens, "head", "cranium", "skull"))
                return BoneRole.Head;

            if (HasAnyToken(tokens, "tail"))
                return BoneRole.Tail;

            // Arms
            if (HasAnyToken(tokens, "upperarm", "shoulder", "humerus", "arm"))
            {
                if (HasAnyToken(tokens, "forearm", "lowerarm"))
                    return isLeft ? BoneRole.LeftLowerArm : isRight ? BoneRole.RightLowerArm : BoneRole.Unknown;

                if (isLeft) return BoneRole.LeftUpperArm;
                if (isRight) return BoneRole.RightUpperArm;
            }

            if (HasAnyToken(tokens, "forearm", "lowerarm", "elbow", "ulna"))
            {
                if (isLeft) return BoneRole.LeftLowerArm;
                if (isRight) return BoneRole.RightLowerArm;
            }

            // Legs
            if (HasAnyToken(tokens, "thigh", "upperleg", "femur", "leg"))
            {
                if (HasAnyToken(tokens, "lowerleg", "shin", "calf"))
                    return isLeft ? BoneRole.LeftLowerLeg : isRight ? BoneRole.RightLowerLeg : BoneRole.Unknown;

                if (isLeft) return BoneRole.LeftUpperLeg;
                if (isRight) return BoneRole.RightUpperLeg;
            }

            if (HasAnyToken(tokens, "calf", "shin", "lowerleg", "knee", "tibia"))
            {
                if (isLeft) return BoneRole.LeftLowerLeg;
                if (isRight) return BoneRole.RightLowerLeg;
            }

            return BoneRole.Unknown;
        }

        private static bool HasAnyToken(string[] tokens, params string[] candidates)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string tok = StripTrailingDigits(tokens[i]);
                for (int j = 0; j < candidates.Length; j++)
                {
                    if (tok == candidates[j])
                        return true;
                }
            }
            return false;
        }

        private static string StripTrailingDigits(string token)
        {
            int end = token.Length;
            while (end > 0 && char.IsDigit(token[end - 1]))
                end--;
            return end == token.Length ? token : token.Substring(0, end);
        }
    }
}
