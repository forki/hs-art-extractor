﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HearthstoneDisunity.Unity;
using HearthstoneDisunity.Unity.Objects;
using HearthstoneDisunity.Util;

namespace HearthstoneDisunity.Hearthstone.Bundle
{
    public class CardsBundle
    {
        public List<CardArtOld> CardArtOld { get; private set; }
        public Dictionary<string, List<CardArtOld>> CardArtOldByTexture { get; private set; }

        private AssestFile _bundle;
        private Dictionary<long, ObjectInfo> _bundleObjects;

        public CardsBundle()
        {
            CardArtOld = new List<CardArtOld>();
            CardArtOldByTexture = new Dictionary<string, List<CardArtOld>>();
            // TODO: this is useless
        }

        public CardsBundle(AssestFile bundle) : this()
        {
            _bundle = bundle;
            _bundleObjects = bundle.ObjectMap;
            Process();
        }

        private void Process()
        {
            Dictionary<long, object> fileMap = new Dictionary<long, object>();
            List<GameObject> gameObjects = new List<GameObject>();

            foreach (var pair in _bundleObjects)
            {
                var info = pair.Value;
                var id = pair.Key;
                Debug.Assert(!fileMap.ContainsKey(id));

                //var data = BinaryBlock.CreateFromByteArray(objectData.Buffer);
                try
                {
                    // TODO: open/close for every object! just seek
                    using (BinaryBlock b = new BinaryBlock(File.Open(_bundle.FilePath, FileMode.Open)))
                    {
                        b.Seek(info.Offset + _bundle.DataOffset);
                        switch (info.ClassId)
                        {
                            case 1: // GameObject
                                var go = new GameObject(b);
                                fileMap[id] = go;
                                gameObjects.Add(go);
                                break;

                            case 4: // Transform
                                fileMap[id] = new Transform(b);
                                break;

                            case 21: // Material
                                fileMap[id] = new Material(b);
                                break;

                            case 28: // Texture2D
                                fileMap[id] = new Texture2D(b);
                                break;

                            case 114: // MonoBehaviour
                                fileMap[id] = new CardDef(b);
                                break;

                            default:
                                break;
                        }
                    }
                }
                catch (Exception e) { throw e; }
            }

            foreach (var go in gameObjects)
            {
                if (!string.IsNullOrWhiteSpace(go.Name))
                {
                    CardArtOld card = new CardArtOld();
                    card.Name = go.Name;

                    foreach (var fp in go.Components)
                    {
                        if (fp.ClassID == 114) // CardDef in this case
                        {
                            if (fileMap.ContainsKey(fp.PathID))
                            {
                                CardDef cd = (CardDef)fileMap[fp.PathID];
                                card.PortraitPath = cd.PortratitTexturePath;
                                var em = cd.EnchantmentPortrait;
                                var dm = cd.DeckCardBarPortrait;
                                if (fileMap.ContainsKey(em.PathID))
                                    card.Portrait = new CardMaterial((Material)fileMap[em.PathID]);
                                if (fileMap.ContainsKey(dm.PathID))
                                    card.DeckBar = new CardMaterial((Material)fileMap[dm.PathID]);

                                if (card.PortraitName != null)
                                {
                                    if (!CardArtOldByTexture.ContainsKey(card.PortraitName))
                                    {
                                        CardArtOldByTexture[card.PortraitName] = new List<CardArtOld>();
                                    }
                                    CardArtOldByTexture[card.PortraitName].Add(card);

                                    CardArtOld.Add(card);

                                    //// save text info by cardid
                                    //var outFile = Path.Combine(dir, card.Name + ".txt");
                                    //// TODO: duplicate check, => rename _2
                                    //using(StreamWriter sw = new StreamWriter(outFile, false))
                                    //{
                                    //    sw.WriteLine(fp.PathID);
                                    //    sw.Write(DebugUtils.AllPropsToString(card));
                                    //}
                                }
                                else
                                {
                                    // TODO: list all with now portrait name
                                }
                            }
                            else
                            {
                                Console.WriteLine("Not found in filemap: " + fp.PathID);
                            }
                        }
                    }
                }
            }
        }
    }
}