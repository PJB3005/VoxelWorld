﻿using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;
using LZ4pn;

namespace VoxelWorld
{
    // Holds voxel data for a room
    internal class VoxelMap
    {
        private byte[][] lz4Chunks;
        private bool[] subchunks;
        private byte[] lightCookieData;
        private bool loadingVoxels;
        private readonly object syncRoot = new object();

        private static CachedChunk[] chunkCache;
        private static Dictionary<IntVector3, int> chunkCacheIndices = new Dictionary<IntVector3, int>();
        private static int cacheAge;
        private static int totalGetVoxels;

        private int xVoxels;
        private int yVoxels;
        private int zVoxels;
        private int xSubchunks;
        private int ySubchunks;
        private int zSubchunks;
        private IntVector2 displayOffset;

        public readonly Room room;

        public string FilePath { get; }
        public void GetVoxels(byte[] buffer, int chunkX, int chunkY, out int width, out int height, out int depth)
        {
            WaitForVoxels();
            DecompressChunk(lz4Chunks[chunkX + chunkY * XChunks], buffer, xVoxels, yVoxels, chunkX, chunkY, out width, out height, out depth);
        }
        public byte[] GetVoxelsCached(int chunkX, int chunkY, out int width, out int height, out int depth)
        {
            totalGetVoxels++;

            if(chunkCache == null || chunkCache.Length != Preferences.maxCachedChunks)
            {
                chunkCache = new CachedChunk[Preferences.maxCachedChunks];
                chunkCacheIndices.Clear();
            }

            // Check the cache for this chunk
            var key = new IntVector3(chunkX, chunkY, room.abstractRoom.index);
            if(!chunkCacheIndices.TryGetValue(key, out int index))
            {
                // Find the oldest chunk in the cache and replace it
                index = 0;
                for(int i = 1; i < chunkCache.Length; i++)
                {
                    if (chunkCache[i].age < chunkCache[index].age)
                        index = i;
                }

                var oldKey = chunkCache[index].pos;
                chunkCacheIndices.Remove(chunkCache[index].pos);
                chunkCacheIndices[key] = index;

                var buffer = chunkCache[index].data ?? new byte[Preferences.chunkSize * Preferences.chunkSize * 30];
                GetVoxels(buffer, chunkX, chunkY, out width, out height, out depth);
                chunkCache[index] = new CachedChunk(key, buffer, width, height, depth);

                //Debug.Log($"Read {totalGetVoxels} ({cacheAge}): ({key.x},{key.y},{key.z}) -> Slot {index}, replacing ({oldKey.x},{oldKey.y},{oldKey.z})");
            }
            else
            {
                width = chunkCache[index].width;
                height = chunkCache[index].height;
                depth = chunkCache[index].depth;
            }

            if (chunkCache[index].age != cacheAge || cacheAge == 0)
                chunkCache[index].age = ++cacheAge;

            return chunkCache[index].data;
        }

        private static void DecompressChunk(byte[] lz4Data, byte[] buffer, int xVoxels, int yVoxels, int chunkX, int chunkY, out int width, out int height, out int depth)
        {
            // Bound checks would be redundant
            width = Math.Min(xVoxels - chunkX * Preferences.chunkSize, Preferences.chunkSize);
            height = Math.Min(yVoxels - chunkY * Preferences.chunkSize, Preferences.chunkSize);
            depth = 30;

            LZ4Codec.Decode32(lz4Data, 0, lz4Data.Length, buffer, 0, width * height * depth, true);
        }

        public int XVoxels
        {
            get
            {
                WaitForVoxels();
                return xVoxels;
            }
        }
        public int YVoxels
        {
            get
            {
                WaitForVoxels();
                return yVoxels;
            }
        }
        public int ZVoxels
        {
            get
            {
                WaitForVoxels();
                return zVoxels;
            }
        }
        public int XChunks => (XVoxels + Preferences.chunkSize - 1) / Preferences.chunkSize;
        public int YChunks => (YVoxels + Preferences.chunkSize - 1) / Preferences.chunkSize;

        public IntVector2 DisplayOffset
        {
            get
            {
                WaitForVoxels();
                return displayOffset;
            }
        }
        public bool HasVoxelMap => loadingVoxels || lz4Chunks != null;

        public bool[] Subchunks
        {
            get
            {
                WaitForVoxels();
                return subchunks;
            }
        }
        public int XSubchunks
        {
            get
            {
                WaitForVoxels();
                return xSubchunks;
            }
        }
        public int YSubchunks
        {
            get
            {
                WaitForVoxels();
                return ySubchunks;
            }
        }
        public int ZSubchunks
        {
            get
            {
                WaitForVoxels();
                return zSubchunks;
            }
        }
        public byte[] LightCookieData
        {
            get
            {
                WaitForVoxels();
                return lightCookieData;
            }
        }

        public VoxelMap(Room room)
        {
            Debug.Log($"Created voxel map: {room.abstractRoom.name}");

            this.room = room;
            string roomName = room.abstractRoom.name;

            FilePath = VoxelWorld.GetRoomFilePath(roomName, "_Voxels.vx1.gz");
            if (File.Exists(FilePath))
            {
                loadingVoxels = true;
                ThreadPool.QueueUserWorkItem(state => {
                    try
                    {
                        LoadVoxels();
                    }
                    catch(Exception e)
                    {
                        VoxelWorld.LogThreaded(new Exception("Failed to call LoadVoxels!", e));
                        loadingVoxels = false;
                    }
                }, null);
            }
        }

        private void WaitForVoxels()
        {
            while (loadingVoxels)
                Thread.Sleep(0);
        }

        public void LoadVoxels()
        {
            byte[][] outLZ4Chunks = null;
            bool[] outSubchunks = null;
            try
            {
                // Unzip voxel file
                var br = new BinaryReader(new Ionic.Zlib.GZipStream(File.OpenRead(FilePath), Ionic.Zlib.CompressionMode.Decompress, Ionic.Zlib.CompressionLevel.BestCompression));

                int width = xVoxels = br.ReadUInt16();
                int height = yVoxels = br.ReadUInt16();
                int depth = zVoxels = br.ReadUInt16();

                int xChunks = (width + Preferences.chunkSize - 1) / Preferences.chunkSize;
                int yChunks = (height + Preferences.chunkSize - 1) / Preferences.chunkSize;

                VoxelWorld.LogThreaded($"Loading {width}x{height}x{depth} voxel map...");

                displayOffset = new IntVector2(
                    br.ReadInt16(),
                    br.ReadInt16()
                );

                int scSize = Preferences.subchunkSize;

                int scsWidth = xSubchunks = (width + scSize - 1) / scSize;
                int scsHeight = ySubchunks = (height + scSize - 1) / scSize;
                int scsDepth = zSubchunks = (depth + scSize - 1) / scSize;

                // Load voxels
                outLZ4Chunks = new byte[xChunks * yChunks][];
                outSubchunks = new bool[scsWidth * scsHeight * scsDepth];

                {
                    byte[] rawChunk = new byte[Preferences.chunkSize * Preferences.chunkSize * 30];
                    for (int chunkI = 0; chunkI < xChunks * yChunks; chunkI++)
                    {
                        int chunkX = chunkI % xChunks;
                        int chunkY = chunkI / xChunks;

                        // Read LZ4 encoded voxels prefixed with data length
                        int len = br.Read7BitEncodedInt();
                        outLZ4Chunks[chunkI] = new byte[len];
                        br.Read(outLZ4Chunks[chunkI], 0, len);

                        // Decode temporarily to fill subchunks
                        DecompressChunk(outLZ4Chunks[chunkI], rawChunk, width, height, chunkX, chunkY, out int chunkW, out int chunkH, out int chunkD);

                        // Each subchunk is true if any voxels within it are solid
                        int originX = chunkX * Preferences.chunkSize;
                        int originY = chunkY * Preferences.chunkSize;
                        int i = 0;
                        for (int z = 0; z < chunkD; z++)
                        {
                            int scZOffset = z / scSize * scsWidth * scsHeight;
                            for (int y = 0; y < chunkH; y++)
                            {
                                int scYOffset = (y + originY) / scSize * scsWidth;
                                for (int x = 0; x < chunkW; x++)
                                {
                                    if (rawChunk[i++] != 0)
                                        outSubchunks[(x + originX) / scSize + scYOffset + scZOffset] = true;
                                }
                            }
                        }
                    }
                }

                // br is now at the start of the light image
                try
                {
                    int imgLen = br.Read7BitEncodedInt();
                    if (imgLen > 0)
                    {
                        byte[] imgData = new byte[imgLen];
                        br.Read(imgData, 0, imgLen);
                        lightCookieData = imgData;
                    }
                }
                catch (Exception e)
                {
                    VoxelWorld.LogThreaded($"Couldn't find light image: {e}");
                }

                int full = 0;
                int empty = 0;
                foreach (var b in outSubchunks)
                {
                    if (b) full++;
                    else empty++;
                }
                VoxelWorld.LogThreaded($"Loaded: {full} full subchunks, {empty} empty subchunks");

                VoxelWorld.LogThreaded($"Successfully loaded voxel map: {room.abstractRoom.name}");

                try
                {
                    br.Close();
                }
                catch(Exception e)
                {
                    VoxelWorld.LogThreaded(e);
                }
            }
            catch (System.IO.IsolatedStorage.IsolatedStorageException) { }
            catch (Exception e)
            {
                VoxelWorld.LogThreaded(new Exception("Failed to get voxel map!", e));
                lz4Chunks = null;
                subchunks = null;
                xVoxels = 0;
                yVoxels = 0;
                zVoxels = 0;
                xSubchunks = 0;
                ySubchunks = 0;
                zSubchunks = 0;
            }

            lock (syncRoot)
            {
                if (loadingVoxels)
                {
                    loadingVoxels = false;
                    lz4Chunks = outLZ4Chunks;
                    subchunks = outSubchunks;
                }
            }
        }

        public int GetDepth(int x, int y)
        {
            x = Mathf.Clamp(x, 0, XVoxels - 1);
            y = Mathf.Clamp(y, 0, YVoxels - 1);

            int chunkX = x / Preferences.chunkSize;
            int chunkY = y / Preferences.chunkSize;

            byte[] data = GetVoxelsCached(chunkX, chunkY, out int w, out int h, out int d);
            x = Mathf.Clamp(x % Preferences.chunkSize, 0, w);
            y = Mathf.Clamp(y % Preferences.chunkSize, 0, h);

            int z = 0;
            while (z < d && data[x + y * w + z * w * h] == 0)
                z++;
            return z;
        }

        public bool IsSolid(int x, int y, int z)
        {
            return GetPaletteColor(GetVoxel(x, y, z)) > 0;
        }

        public static int GetPaletteColor(byte voxel) => voxel & 0x3;
        public static int GetEffectColor(byte voxel) => (voxel >> 2) & 0x3;
        public static float GetEffectIntensity(byte voxel) => ((voxel >> 4) & 0xF) / (float)0xF;

        public byte GetVoxel(int x, int y, int z)
        {
            x = Mathf.Clamp(x, 0, XVoxels - 1);
            y = Mathf.Clamp(y, 0, YVoxels - 1);
            z = Mathf.Clamp(z, 0, ZVoxels - 1);

            int chunkX = x / Preferences.chunkSize;
            int chunkY = y / Preferences.chunkSize;

            byte[] data = GetVoxelsCached(chunkX, chunkY, out int w, out int h, out int d);
            x = Mathf.Clamp(x % Preferences.chunkSize, 0, w);
            y = Mathf.Clamp(y % Preferences.chunkSize, 0, h);
            z = Mathf.Clamp(z, 0, d);

            return data[x + y * w + z * w * h];
        }

        public void Update()
        {
        }

        public void Unload()
        {
            lock (syncRoot)
            {
                lz4Chunks = null;
            }
        }

        private struct CachedChunk
        {
            public IntVector3 pos;
            public int age;
            public byte[] data;
            public int width;
            public int height;
            public int depth;

            public CachedChunk(IntVector3 pos, byte[] data, int width, int height, int depth)
            {
                this.pos = pos;
                age = 0;
                this.data = data;
                this.width = width;
                this.height = height;
                this.depth = depth;
            }
        }
    }
}
