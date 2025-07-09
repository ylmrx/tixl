using T3.Core.Animation;
using T3.Core.Utils;
using T3.Core.Video;

namespace Lib.flow.testing;

[Guid("5441cf42-3e48-4bfc-9b14-f8fdf87cefdc")]
internal sealed class SaveImage : Instance<SaveImage>
{
    [Output(Guid = "F45FA3D4-8103-4AB3-B709-2D092E023A90")]
    public readonly Slot<Command> Command = new();

    public SaveImage()
    {
        Command.UpdateAction = Update;
    }

    
    
    private void Update(EvaluationContext context)
    {
        ScreenshotWriter.Update();
        
        var hasChanged = MathUtils.WasChanged(TriggerSave.GetValue(context), ref _triggered);
        if (!hasChanged)
            return;

        if (_triggered)
        {
            var image = Image.GetValue(context);
            var filepath = FilePath.GetValue(context);
            Log.Debug("Saving image " + filepath );
            ScreenshotWriter.StartSavingToFile(image,filepath, ScreenshotWriter.FileFormats.Png);
            
        }

        // Command.Value = _textureResource.GetValue(context);
        // Command.DirtyFlag.Clear();
        //
        // if (Command.Value == null)
        // {
        //     _lastErrorMessage = "Failed to load texture: " + Path.Value;
        //     Log.Warning(_lastErrorMessage, this);
        //     return;
        // }
        //
        // var currentSrv = SrvManager.GetSrvForTexture(Command.Value);
        //
        // try
        // {
        //     ResourceManager.Device.ImmediateContext.GenerateMips(currentSrv);
        // }
        // catch (Exception exception)
        // {
        //     Log.Error($"Failed to generate mipmaps for texture {Path.Value}:" + exception);
        // }
        //
        // _lastErrorMessage = string.Empty;


    }

    private bool _triggered;

    [Input(Guid = "55404F84-2D5B-481C-B21E-60447015449B")]
    public readonly InputSlot<Texture2D> Image = new();

    [Input(Guid = "72C6F29A-6078-4155-A6FA-2340A03707FB")]
    public readonly InputSlot<string> FilePath = new();
    
    [Input(Guid = "01470496-4DFE-45FB-A306-001422A4AF92")]
    public readonly InputSlot<bool> TriggerSave = new();
    
    // public IEnumerable<string> FileFilter => FileFilters;
    // private static readonly string[] FileFilters = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.dds", "*.gif"];
    // public InputSlot<string> SourcePathSlot => Path;

    //private readonly Resource<Texture2D> _textureResource;

    // IStatusProvider.StatusLevel IStatusProvider.GetStatusLevel() =>
    //     string.IsNullOrEmpty(_lastErrorMessage) ? IStatusProvider.StatusLevel.Success : IStatusProvider.StatusLevel.Warning;

    // string IStatusProvider.GetStatusMessage() => _lastErrorMessage;
    //
    // private string _lastErrorMessage = string.Empty;
}