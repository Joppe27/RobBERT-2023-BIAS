// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

namespace RobBERT_2023_BIAS.Inference;

public class RobbertPrompt(string sentence, string? wordToMask = null, string? wordToDecode = null)
{
    public string Sentence { get; set; } = sentence;

    public string? WordToMask { get; set; } = wordToMask;

    public string? WordToDecode { get; set; } = wordToDecode;
}