using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    //Double buffering system

    public class DoubleBufferSystem
    {
        private int front = 0;
        private RenderTargetIdentifier[] colorIds;
        private RenderTargetIdentifier depthId;
        public RenderTextureDescriptor colorDesc;

        public RenderTargetIdentifier FrontColorId
        {
            set { colorIds[front] = value; }
            get { return colorIds[front]; }
        }

        public RenderTargetIdentifier BackColorId
        {
            set { colorIds[1 - front] = value; }
            get { return colorIds[1 - front]; }
        }

        public RenderTargetIdentifier DepthId
        {
            set
            {
                depthId = value;
            }
            get
            {
                return depthId;
            }
        }

        private bool allocated = false;

        public void Create(RenderTargetIdentifier sourceColorId, RenderTargetIdentifier sourceDepthId, int width, int height)
        {
            if (allocated)
                return;
            allocated = true;
            front = 0;
            colorIds = new RenderTargetIdentifier[2];
            FrontColorId = sourceColorId;

            colorDesc = new RenderTextureDescriptor(width, height);
            colorDesc.depthBufferBits = 0;
            colorDesc.colorFormat = RenderTextureFormat.Default;

            DepthId = sourceDepthId;
            
            BackColorId = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default);
        }

        public void Swap()
        {
            front = 1 - front;
        }
    }
}