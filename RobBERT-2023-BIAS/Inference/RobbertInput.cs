// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

namespace RobBERT_2023_BIAS.Inference;

public struct RobbertInput
{
    public long[] InputIds { get; set; }
    public long[] AttentionMask { get; set; }
}