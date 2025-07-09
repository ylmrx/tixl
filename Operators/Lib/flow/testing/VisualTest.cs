using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.WIC;
using T3.Core.Video;
using Utilities = T3.Core.Utils.Utilities;

namespace Lib.flow.testing;

[Guid("f92a1144-55d9-4d8e-bfa6-f80665800e25")]
internal sealed class VisualTest : Instance<VisualTest>
{
    [Output(Guid = "96222dc9-aa23-4c85-99f1-08fd1107d579")]
    public readonly Slot<Command> Command = new();

    public VisualTest()
    {
        Command.UpdateAction = Update;
    }

    private void Update(EvaluationContext context)
    {
        var testframeId = "_TestFrame";
        var testResultId = "_TestResult";
        var testActionId = "_TestAction";

        var isExecution = context.IntVariables.TryGetValue(testframeId, out var testFrame);
        if (!isExecution)
        {
            Log.Warning("Needs to be run with [ExecuteTests]", this);
            return;
        }

        if (!context.ObjectVariables.TryGetValue(testResultId, out var obj2) || obj2 is not StringBuilder testResult)
        {
            Log.Warning("Test Results missing?", this);
            return;
        }

        if (!context.ObjectVariables.TryGetValue(testActionId, out var obj) || obj is not string testAction)
        {
            Log.Warning("Test Action missing?", this);
            return;
        }

        _testName = TestName.GetValue(context);
        
        var image = Image.GetValue(context);
        //var image2 = Image2.GetValue(context);

        if (image == null)
        {
            Log.Warning("Image missing?", this);
            return;
        }
        
        switch (testAction)
        {
            case "Test":
            {
                SharpDX.Direct3D11.Texture2D diffColorImage = null;

                var image2 = LoadTextureFromFile( ResourceManager.Device,GetReferenceFilepath(0, 0));
                
                // TODO:
                var threshold = 0.1f;
                float deviation = CompareImage(image, image2, ref diffColorImage);
                if (deviation < threshold)
                {
                    //RemoveFailureImages(index, count);
                    //result = true;
                }
                else
                {
                    // TODO
                    //StoreImages(index, count, _offscreenRenderer.ColorImage, diffColorImage);
                    //result = false;
                }
                var compareResultString = $"Difference {GetReferenceFilepath(0,0)}: {deviation:0.00}";
                testResult.AppendLine(compareResultString);
                
                //Utilities.Dispose(ref referenceImage);
                Utilities.Dispose(ref diffColorImage);
                //return result;
                
                Log.Debug("Executing", this);
                //CompareImage(image, image);
                break;
            }

            case "UpdateReferences":
                Log.Debug("Update references", this);
                var filepath = GetReferenceFilepath(0,0);
                Log.Debug("Saving image " + filepath, this);
                ScreenshotWriter.StartSavingToFile(image, filepath, ScreenshotWriter.FileFormats.Png);
                testResult.Append("saved " + filepath);
                break;

            default:
                Log.Warning($"Unknown action {testAction}", this);
                break;
        }
    }

    
    void RemoveFailureImages(int index, int count)
    {
        FileInfo fi = new FileInfo(GetReferenceFilepath(index, count));
        string path = Path.GetDirectoryName(fi.FullName);
        string filename = Path.GetFileNameWithoutExtension(fi.FullName);
        string ext = Path.GetExtension(fi.FullName);
        string f1 = String.Format("{0}/{1}.current{2}", path, filename, ext);
        string f2 = String.Format("{0}/{1}.diff{2}", path, filename, ext);
        File.Delete(f1);
        File.Delete(f2);
    }
    
    
    string GetReferenceFilepath(int index, int count)
    {
        //TODO:
        return $"{_testName}{index}_{count}.png";
    }

    private static SharpDX.Direct3D11.Texture2D LoadTextureFromFile(SharpDX.Direct3D11.Device device, string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }
        
        ImagingFactory imagingFactory = new ImagingFactory();

        // Decode the image
        var decoder = new BitmapDecoder(imagingFactory, filePath, DecodeOptions.CacheOnDemand);
        var frame = decoder.GetFrame(0);

        // Convert to 32bpp RGBA
        var converter = new FormatConverter(imagingFactory);
        converter.Initialize(frame, PixelFormat.Format32bppRGBA);

        int width = converter.Size.Width;
        int height = converter.Size.Height;

        // Copy pixels into a buffer
        var stride = width * 4;
        var buffer = new DataStream(height * stride, true, true);
        converter.CopyPixels(stride, buffer);

        // Create texture description
        var texDesc = new Texture2DDescription
                          {
                              Width = width,
                              Height = height,
                              ArraySize = 1,
                              BindFlags = BindFlags.ShaderResource,
                              Usage = ResourceUsage.Immutable,
                              CpuAccessFlags = CpuAccessFlags.None,
                              Format = Format.R8G8B8A8_UNorm,
                              MipLevels = 1,
                              OptionFlags = ResourceOptionFlags.None,
                              SampleDescription = new SampleDescription(1, 0)
                          };

        // Define initial data
        var dataBox = new DataBox(buffer.DataPointer, stride, 0);
        var dataBoxes = new[] { dataBox };
        var texture = new SharpDX.Direct3D11.Texture2D(device, texDesc, dataBoxes);

        // Cleanup
        buffer.Dispose();
        converter.Dispose();
        frame.Dispose();
        decoder.Dispose();
        imagingFactory.Dispose();
        return texture;
    }

    private float CompareImage(SharpDX.Direct3D11.Texture2D current, 
                               SharpDX.Direct3D11.Texture2D reference, 
                               ref SharpDX.Direct3D11.Texture2D differenceImage)
    {
        try
        {
            if (current == null || reference == null ||
                current.Description.Width != reference.Description.Width ||
                current.Description.Height != reference.Description.Height ||
                current.Description.Format != reference.Description.Format)
            {
                return 6666.0f;
            }

            var immediateContext = ResourceManager.Device.ImmediateContext;
            var currentDesc = new Texture2DDescription()
                                  {
                                      BindFlags = BindFlags.None,
                                      Format = current.Description.Format,
                                      Width = current.Description.Width,
                                      Height = current.Description.Height,
                                      MipLevels = current.Description.MipLevels,
                                      SampleDescription = new SampleDescription(1, 0),
                                      Usage = ResourceUsage.Staging,
                                      OptionFlags = ResourceOptionFlags.None,
                                      CpuAccessFlags = CpuAccessFlags.Read,
                                      ArraySize = 1
                                  };
            var currentWithCPUAccess = new SharpDX.Direct3D11.Texture2D(ResourceManager.Device, currentDesc);
            immediateContext.CopyResource(current, currentWithCPUAccess);

            var referenceDesc = new Texture2DDescription()
                                    {
                                        BindFlags = BindFlags.None,
                                        Format = reference.Description.Format,
                                        Width = reference.Description.Width,
                                        Height = reference.Description.Height,
                                        MipLevels = reference.Description.MipLevels,
                                        SampleDescription = new SampleDescription(1, 0),
                                        Usage = ResourceUsage.Staging,
                                        OptionFlags = ResourceOptionFlags.None,
                                        CpuAccessFlags = CpuAccessFlags.Read,
                                        ArraySize = 1
                                    };
            var referenceWithCPUAccess = new SharpDX.Direct3D11.Texture2D(ResourceManager.Device, referenceDesc);
            immediateContext.CopyResource(reference, referenceWithCPUAccess);

            var differenceDesc = new Texture2DDescription()
                                     {
                                         BindFlags = BindFlags.None,
                                         Format = current.Description.Format,
                                         Width = current.Description.Width,
                                         Height = current.Description.Height,
                                         MipLevels = current.Description.MipLevels,
                                         SampleDescription = new SampleDescription(1, 0),
                                         Usage = ResourceUsage.Staging,
                                         OptionFlags = ResourceOptionFlags.None,
                                         CpuAccessFlags = CpuAccessFlags.Write,
                                         ArraySize = 1
                                     };
            Utilities.Dispose(ref differenceImage);

            differenceImage = new SharpDX.Direct3D11.Texture2D(ResourceManager.Device, differenceDesc);

            var currentDataBox = immediateContext.MapSubresource(currentWithCPUAccess, 0, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None, out var currentStream);
            currentStream.Position = 0;

            var refDataBox = immediateContext.MapSubresource(referenceWithCPUAccess, 0, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None, out var referenceStream);
            referenceStream.Position = 0;

            var diffDataBox = immediateContext.MapSubresource(differenceImage, 0, 0, MapMode.Write, SharpDX.Direct3D11.MapFlags.None, out var differenceStream);
            differenceStream.Position = 0;

            double deviation = 0;
            for (int y = 0; y < current.Description.Height; ++y)
            {
                for (int x = 0; x < current.Description.Width; ++x)
                {
                    Color4 currentC = new Color4(currentStream.Read<Int32>());
                    Color4 referenceC = new Color4(referenceStream.Read<Int32>());
                    Color4 diffColor = currentC - referenceC;
                    Color4 absDiffColor = new Color4(Math.Abs(diffColor.Red), Math.Abs(diffColor.Green), Math.Abs(diffColor.Blue), 1.0f);
                    differenceStream.Write(absDiffColor.ToRgba());
                    deviation += Math.Abs(diffColor.Red) + Math.Abs(diffColor.Green) + Math.Abs(diffColor.Blue) + Math.Abs(diffColor.Alpha);
                }

                currentStream.Position += currentDataBox.RowPitch - current.Description.Width * 4;
                referenceStream.Position += refDataBox.RowPitch - current.Description.Width * 4;
                differenceStream.Position += diffDataBox.RowPitch - current.Description.Width * 4;
            }

            deviation /= current.Description.Width * current.Description.Height;

            immediateContext.UnmapSubresource(currentWithCPUAccess, 0);
            Utilities.Dispose(ref currentStream);
            immediateContext.UnmapSubresource(referenceWithCPUAccess, 0);
            Utilities.Dispose(ref referenceStream);
            immediateContext.UnmapSubresource(differenceImage, 0);
            Utilities.Dispose(ref differenceStream);
            Utilities.Dispose(ref currentWithCPUAccess);
            Utilities.Dispose(ref referenceWithCPUAccess);
            return (float)deviation;
        }
        catch (Exception ex)
        {
            Log.Error(" Failed to compare " + ex.Message, this);
            return -1;
        }
    }

    private string _testName = "Test";

    [Input(Guid = "ed9887ca-5ee4-4fb7-a835-071de255a893")]
    public readonly InputSlot<Texture2D> Image = new();

    
    [Input(Guid = "6c6d3909-3f27-43bd-9900-5bf00fb6015b")]
    public readonly InputSlot<string> TestName = new();
}