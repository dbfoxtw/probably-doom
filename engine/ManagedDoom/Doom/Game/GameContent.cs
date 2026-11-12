//
// Probably Doom 修改（2026-11-12）：WAD 改由宿主讀好交進來（WadFile），不再照命令列或執行檔旁找檔案。
//
// Copyright (C) 1993-1996 Id Software, Inc.
// Copyright (C) 2019-2020 Nobuaki Tanaka
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//



using System;

namespace ManagedDoom
{
    public sealed class GameContent : IDisposable
    {
        private Wad wad;
        private Palette palette;
        private ColorMap colorMap;
        private ITextureLookup textures;
        private IFlatLookup flats;
        private ISpriteLookup sprites;
        private TextureAnimation animation;

        private GameContent()
        {
        }

        // Probably Doom：WAD 由宿主讀好交進來（第一個是 IWAD），不再照命令列的 -iwad／-file 或執行檔旁找檔案。
        public GameContent(CommandLineArgs args, params WadFile[] wads)
        {
            if (wads == null || wads.Length == 0)
            {
                throw new Exception("No IWAD was given!");
            }

            wad = new Wad(wads);

            DeHackEd.Initialize(args, wad);

            palette = new Palette(wad);
            colorMap = new ColorMap(wad);
            textures = new TextureLookup(wad);
            flats = new FlatLookup(wad);
            sprites = new SpriteLookup(wad);
            animation = new TextureAnimation(textures, flats);
        }

        public static GameContent CreateDummy(params WadFile[] wads)
        {
            var gc = new GameContent();

            gc.wad = new Wad(wads);
            gc.palette = new Palette(gc.wad);
            gc.colorMap = new ColorMap(gc.wad);
            gc.textures = new DummyTextureLookup(gc.wad);
            gc.flats = new DummyFlatLookup(gc.wad);
            gc.sprites = new DummySpriteLookup(gc.wad);
            gc.animation = new TextureAnimation(gc.textures, gc.flats);

            return gc;
        }

        public void Dispose()
        {
            if (wad != null)
            {
                wad.Dispose();
                wad = null;
            }
        }

        public Wad Wad => wad;
        public Palette Palette => palette;
        public ColorMap ColorMap => colorMap;
        public ITextureLookup Textures => textures;
        public IFlatLookup Flats => flats;
        public ISpriteLookup Sprites => sprites;
        public TextureAnimation Animation => animation;
    }
}
