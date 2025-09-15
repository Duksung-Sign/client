using UnityEngine;
using UnityEngine.Playables;

public class PauseClip : PlayableAsset
{
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<PauseBehaviour>.Create(graph);
    }
}