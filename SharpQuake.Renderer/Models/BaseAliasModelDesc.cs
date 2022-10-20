namespace SharpQuake.Renderer.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class BaseAliasModelDesc : BaseModelDesc
    {
        public virtual int AliasFrame
        {
            get;
            set;
        }

        // model animation interpolation
        public virtual int LastPoseNumber0
        {
            get;
            set;
        }

        public virtual int LastPoseNumber
        {
            get;
            set;
        }
    }
}
