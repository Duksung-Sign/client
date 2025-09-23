using UnityEngine;
using UnityEngine.Timeline;

[TrackBindingType(typeof(EmergencyDialogueManager))]
[TrackClipType(typeof(DialogueClip))]
[TrackClipType(typeof(PauseClip))] 
public class DialogueTrack : TrackAsset { }