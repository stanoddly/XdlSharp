using static XdlSharp.Xdl;

// Each version call loads one library of the native package, and the satellites load XDL3 next to them.
(string Name, int Loaded, int Header)[] libraries =
[
    ("XDL3", SDL_GetVersion(), SDL_VERSION),
    ("XDL3_image", IMG_Version(), SDL_IMAGE_VERSION),
    ("XDL3_ttf", TTF_Version(), SDL_TTF_VERSION),
    ("XDL3_mixer", MIX_Version(), SDL_MIXER_VERSION),
];

foreach ((string name, int loaded, int header) in libraries)
{
    Console.WriteLine($"{name}: {loaded}, the header says {header}");
}

return libraries.All(library => library.Loaded == library.Header) ? 0 : 1;
