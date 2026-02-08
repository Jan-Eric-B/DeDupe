using System.ComponentModel;

namespace DeDupe.Enums
{
    public enum MediaType
    {
        [Description("Image")]
        Image,

        [Description("Video")]
        Video,

        [Description("Video Frame")]
        VideoFrame
    }
}