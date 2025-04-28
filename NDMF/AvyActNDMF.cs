#if UNITY_EDITOR

using nadena.dev.ndmf;
using A1ST.AvyAct.NDMF;
using A1ST.AvyAct.Processor;

[assembly: ExportsPlugin(typeof(AvyActPlugin))]

// ReSharper disable once CheckNamespace
namespace A1ST.AvyAct.NDMF
{
    public class AvyActPlugin : Plugin<AvyActPlugin>
    {
        public override string QualifiedName => "a1st.AvyAct.NDMF.plugin";
        public override string DisplayName => "AvyAct Modular Avatar Plugin";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .Run(
                    "AvyAct - Preprocess",
                    ctx =>
                    {
                        AvyActProcessor.Preprocess(ctx.AvatarRootObject);
                    }
                );
            InPhase(BuildPhase.Transforming)
                .Run(
                    "AvyAct - Build Layers",
                    ctx =>
                    {
                        AvyActProcessor.Execute(ctx.AvatarRootObject, ctx.AssetContainer);
                    }
                );
            InPhase(BuildPhase.Optimizing)
                .Run(
                    "AvyAct - Cleanup",
                    _ =>
                    {
                        AvyActProcessor.Cleanup();
                    }
                );
        }
    }
}
#endif
