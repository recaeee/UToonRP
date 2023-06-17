using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;
public partial class AdditionalRenderPass
{
    partial void PrepareBuffer();

    partial void PrepareForSceneWindow();

#if UNITY_EDITOR
    
    private string SampleName { get; set; }
    
    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        //对每个摄像机使用不同的Sample Name
        buffer.name = SampleName = passName;
        Profiler.EndSample();
    }
    
#endif
}
