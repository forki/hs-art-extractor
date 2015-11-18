﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HearthstoneDisunity.Util;
using HearthstoneDisunity.Unity.Objects;

namespace HearthstoneDisunity.Unity
{
    public class AssetBundle
	{
		public Dictionary<long, ObjectInfo> ObjectMap { get; set; }
		public long AssetDataOffset { get; private set; }        

        public AssetBundleHeader Header { get; private set; }
        public AssetBundleEntry BundleEntry { get; private set; }
        public AssetHeader AssetHeader { get; private set; }
        public TypeTree TypeTree { get; private set; }
        public ObjectInfoTable InfoTable { get; private set; }

        private string _bundleFile;

        public AssetBundle(string file)
        {
            _bundleFile = file;
            Read(_bundleFile);
        }

        public void ExtractRaw(string dir)
        {
            try
            {
                using (BinaryFileReader b = new BinaryFileReader(File.Open(_bundleFile, FileMode.Open)))
                {
                    foreach (var pair in ObjectMap)
                    {
                        var info = pair.Value;
                        var subdir = UnityClasses.Get(info.ClassId);
                        Directory.CreateDirectory(Path.Combine(dir, subdir));
                        b.Seek(info.Offset + AssetDataOffset);

                        byte[] data = new byte[info.Length];
                        // TODO: can there be loss of precision here, long to int?
                        Debug.Assert(info.Length <= int.MaxValue);
                        b.Read(data, 0, (int)info.Length);

                        var outFile = Path.Combine(dir, subdir, pair.Key + ".bin");
                        File.WriteAllBytes(outFile, data);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }             
        }

        public void ExtractFull(string dir)
        {
            try
            {
                using (BinaryFileReader b = new BinaryFileReader(File.Open(_bundleFile, FileMode.Open)))
                {
                    foreach (var pair in ObjectMap)
                    {
                        var info = pair.Value;
                        var subdir = Path.Combine(dir, UnityClasses.Get(info.ClassId));
                        // TODO: don't create dir if not supported type
                        Directory.CreateDirectory(subdir);

                        b.Seek(info.Offset + AssetDataOffset);
                        byte[] data = new byte[info.Length];
                        // TODO: can there be loss of precision here, long to int?
                        Debug.Assert(info.Length <= int.MaxValue);
                        b.Read(data, 0, (int)info.Length);
                        var block = BinaryFileReader.CreateFromByteArray(data);
                        switch (info.ClassId)
                        {
                            case 1: // GameObject
                                new GameObject(block).Save(subdir, pair.Key.ToString());
                                break;
                            case 4: // Transform
                                new Transform(block).Save(subdir, pair.Key.ToString());
                                break;
                            case 21: // Material
                                new Material(block).Save(subdir, pair.Key.ToString());
                                break;
                            case 28: // Texture2D
                                new Texture2D(block).Save(subdir);
                                break;
                            case 114: // MonoBehaviour
                                // TODO: not only carddef obviously
                                new CardDef(block).Save(subdir, pair.Key.ToString());
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void Read(string file)
		{
			try
			{
				using(BinaryFileReader b = new BinaryFileReader(File.Open(file, FileMode.Open)))
				{
					b.BigEndian = true;

					Header = new AssetBundleHeader(b);
					Console.WriteLine(Header);

					if(Header.NumberOfFiles != 1)
                    {
                        // TODO: handle elsewhere?                        
                        throw new AssetException("Should be exactly one file in HS Asset Bundle");
                    }						

					BundleEntry = new AssetBundleEntry(b);
					Console.WriteLine(BundleEntry);

					// move to bundle file offset
					b.Seek(Header.HeaderSize + BundleEntry.Offset);

					AssetHeader = new AssetHeader(b);
					Console.WriteLine(AssetHeader);
                    
					// TODO: references? UnityVersion object
					var version = AssetHeader.AssetVersion;
					
					// should be little endian
					b.BigEndian = AssetHeader.Endianness == 1 ? true : false;

					if(version < 9)
						b.Seek(AssetHeader.FileSize - AssetHeader.MetadataSize + 1);

					// read the bundle metadata, classes and attributes
					TypeTree = new TypeTree(version);
                    TypeTree.Read(b);

                    // read the asset objects info and offsets
                    InfoTable  = new ObjectInfoTable(version);
                    InfoTable.Read(b);
					// assign
					ObjectMap = InfoTable.InfoMap;

					// Skip this not using this for now
					//FileIdentifierTable fi = new FileIdentifierTable(version);
					//fi.Read(b);

					AssetDataOffset = AssetHeader.DataOffset + Header.DataHeaderSize + Header.HeaderSize;

					// LoadObjects(InfoTable.InfoMap, TypeTree.TypeMap, b);

				}
			}
			catch(Exception e)
			{
				throw e;
			}
		}
       
		private void LoadObjects(Dictionary<long, ObjectInfo> infoMap, Dictionary<int, BaseClass> typeTreeMap, BinaryFileReader b)
		{
			long ofsMin = long.MaxValue;
			long ofsMax = long.MinValue;

			List<ObjectData> objectList = new List<ObjectData>();
			List<ObjectData> objectListBroken = new List<ObjectData>();

			foreach(var oi in infoMap)
			{
				ObjectInfo info = oi.Value;

				long id = oi.Key;
				long ofs = AssetDataOffset + info.Offset;
				ofsMin = Math.Min(ofsMin, ofs);
				ofsMax = Math.Max(ofsMax, ofs + info.Length);

				b.Seek(ofs);

				// TODO: can there be loss of precision here, long to int?
				Debug.Assert(info.Length <= int.MaxValue);

				byte[] bytes = new byte[info.Length];
				b.Read(bytes, 0, (int)info.Length); 
				 
				AssetTypeNode typeNode = null;
				BaseClass typeClass = typeTreeMap[info.TypeId];
				if(typeClass != null)
				{
					typeNode = typeClass.TypeTree;
				}
				//if(typeNode == null) // get from database               

				ObjectData data = new ObjectData(id);
				data.Info = info;
				data.Buffer = bytes;
				data.TypeTree = typeNode;
				
				// Add typeless objects to an internal list. 
				// Not interested in them for this implementation.
				if(typeNode == null)
				{
					// log warning if it's not a MonoBehaviour
					if(info.ClassId != 114)
					{
						Console.WriteLine("{0} has no type information", data);
					}
					objectListBroken.Add(data);
				}
				else
				{
					objectList.Add(data);
				}

			}

			// DEBUG
			Console.WriteLine(objectListBroken.Count + " broken objects");

			Dictionary<long, object> fileMap = new Dictionary<long, object>();
			List<GameObject> gameObjects = new List<GameObject>();

            // Extraction
            //if (extractionType == 0)
            //{
            //    // Raw
            //    File.WriteAllBytes(@"E:\Dump\DisunityTest\" + id + ".bin", bytes);
            //}
            //else if (extractionType == 2)
            //{
            //    // CardArt ??
            //}
            //else
            //{
            //    // Extract Supported Types
            //}

            foreach (ObjectData objectData in objectList) {
				Debug.Assert(!fileMap.ContainsKey(objectData.Id));

				var data = BinaryFileReader.CreateFromByteArray(objectData.Buffer);
				switch(objectData.Info.ClassId)
				{
				    case 1: // GameObject
					    var go = new GameObject(data);
					    fileMap[objectData.Id] = go;
					    gameObjects.Add(go);
					    break;
				    case 4: // Transform
					    fileMap[objectData.Id] = new Transform(data);
					    break;
				    case 21: // Material
					    fileMap[objectData.Id] = new Material(data);
					    break;
				    case 28: // Texture2D
					    fileMap[objectData.Id] = new Texture2D(data);
					    break;
				    case 114: // MonoBehaviour
					    fileMap[objectData.Id] = new CardDef(data);
					    break;
				    default:
					    break;
				}
			}

			foreach(var go in gameObjects)
			{				
				if(!string.IsNullOrWhiteSpace(go.Name))
				{
					CardObject card = new CardObject();
					card.Name = go.Name;
					
					foreach(var fp in go.Components)
					{
						if (fp.ClassID == 114) 
						{
							if(fileMap.ContainsKey(fp.PathID))
							{
								CardDef cd = (CardDef)fileMap[fp.PathID];
								card.PortraitPath = cd.PortratitTexturePath;
								var em = cd.EnchantmentPortrait;
								var dm = cd.DeckCardBarPortrait;
								if(fileMap.ContainsKey(em.PathID))
									card.SetEnchantment(fileMap[em.PathID]);
								if(fileMap.ContainsKey(dm.PathID))
									card.SetCardBar(fileMap[dm.PathID]);
							}
							else
							{
								Console.WriteLine("Not found in filemap: " + fp.PathID);
							}
						}						
					}

					Console.WriteLine(card);
				}
			}

				//if(objectData.Info.ClassId == 4)
				//{
				//	Console.WriteLine("-- Transform [" + objectData.Id + "]");
				//	objectData.SaveRaw(@"E:\Dump\DisunityTest\" + objectData.Id + ".trans.bin");
				//	var t = new Transform(BinaryFileReader.CreateFromByteArray(objectData.Buffer));
				//	Console.WriteLine(DebugUtils.AllPropsToString(t));
				//}
				//else if(objectData.Info.ClassId == 1)
				//{
				//	Console.WriteLine("-- GameObject [" + objectData.Id + "]");
				//	objectData.SaveRaw(@"E:\Dump\DisunityTest\" + objectData.Id + ".go.bin");
				//	var t = new GameObject(BinaryFileReader.CreateFromByteArray(objectData.Buffer));
				//	Console.WriteLine(DebugUtils.AllPropsToString(t));
				//}
				//else if(objectData.Info.ClassId == 21)
				//{
				//	Console.WriteLine("-- Material [" + objectData.Id + "]");
				//	objectData.SaveRaw(@"E:\Dump\DisunityTest\" + objectData.Id + ".mat.bin");
				//	var t = new Material(BinaryFileReader.CreateFromByteArray(objectData.Buffer));
				//	Console.WriteLine(DebugUtils.AllPropsToString(t));
				//}
				//else if(objectData.Info.ClassId == 114)
				//{
				//	Console.WriteLine("-- MonoBehaviour [" + objectData.Id + "]");
				//	objectData.SaveRaw(@"E:\Dump\DisunityTest\" + objectData.Id + ".mb.bin");
				//	var t = new CardDef(BinaryFileReader.CreateFromByteArray(objectData.Buffer));
				//	Console.WriteLine(DebugUtils.AllPropsToString(t));
				//}


			//Dictionary<string, long> CardIdMap = new Dictionary<string, long>();
			//Dictionary<long, string> ArtFileMap = new Dictionary<long, string>();

			//foreach(var item in objectList)
			//{
			//	if(item.Info.ClassId == 1)
			//	{
			//		var go = GameObjectName(b, item, assHeader.DataOffset + bundleHeader.DataHeaderSize + bundleHeader.HeaderSize);
			//		Console.WriteLine("{0}: {1}", go.Item1, go.Item2);
			//	}
			//	else if(item.Info.ClassId == 114)
			//	{
			//		var mb = MonoBehaviour(b, item, assHeader.DataOffset + bundleHeader.DataHeaderSize + bundleHeader.HeaderSize);
			//		Console.WriteLine("{0}: {1}", item.Id, mb);
			//	}
			//}
		}
	}
}
