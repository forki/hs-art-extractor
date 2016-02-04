﻿using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using HsArtExtractor.Util;

namespace HsArtExtractor.Hearthstone.CardArt
{
	public class ArtCard
	{
		[XmlAttribute("id")]
		public string Id { get; set; }

		[XmlElement("Texture")]
		public Texture Texture { get; set; }

		[XmlElement("Material")]
		public List<Material> Materials { get; set; }

		public ArtCard()
		{
			Id = "";
			Texture = new Texture();
			Materials = new List<Material>();
		}

		public ArtCard(string id,
			Unity.Objects.CardDef def,
			Unity.Objects.Material portrait,
			Unity.Objects.Material bar)
			: this()
		{
			Id = id;
			if (def != null)
			{
				Texture.Path = def.PortratitTexturePath;
				Texture.Name = StringUtils.GetFilenameNoExt(def.PortratitTexturePath);
			}
			if (portrait != null)
				AddMaterial(portrait, MaterialType.Portrait);
			if (bar != null)
				AddMaterial(bar, MaterialType.CardBar);
		}

		public void AddMaterial(Unity.Objects.Material material, MaterialType type)
		{
			var mat = new Material();
			mat.Type = type;
			mat.AddTransform(material.Floats);
			mat.AddTransform(material.TexEnvs);
			Materials.Add(mat);
		}

		public Material GetMaterial(MaterialType type)
		{
			return Materials.FirstOrDefault(x => x.Type == type);
		}
	}
}