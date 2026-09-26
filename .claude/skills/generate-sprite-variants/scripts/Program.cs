// Usage: dotnet run --project <this folder> --artifacts-path <scratch dir> -- <input image> <output dir> <baseName>
if (args.Length != 3)
{
    System.Console.Error.WriteLine("usage: <input image> <output dir> <baseName>");
    return 1;
}
Bladehold.Tools.SpriteVariantsProcessor.ProcessFile(args[0], args[1], args[2]);
System.Console.WriteLine($"Wrote {args[2]}_Clean/_Stroke/_Underlay/_Embossed/_Sunken.png + {args[2]}.svg to {args[1]}");
return 0;
