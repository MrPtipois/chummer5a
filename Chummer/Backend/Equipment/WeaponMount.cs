﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace Chummer.Backend.Equipment
{
	/// <summary>
	/// Vehicle Modification.
	/// </summary>
	public class WeaponMount : INamedItemWithGuid
	{
		private Guid _guiID;
		private int _intMarkup;
		private string _strAvail = string.Empty;
		private string _strSource = string.Empty;
		private string _strPage = string.Empty;
		private bool _blnIncludeInVehicle;
		private bool _blnInstalled = true;
		private List<Weapon> _weapons = new List<Weapon>();
		private string _strNotes = string.Empty;
		private string _strAltName = string.Empty;
		private string _strAltCategory = string.Empty;
		private string _strAltPage = string.Empty;
		private string _strExtra = string.Empty;
		private string _strWeaponMountCategories = string.Empty;
		private bool _blnDiscountCost;
		private string _strName = string.Empty;
		private string _strCategory = string.Empty;
		private string _strLimit = string.Empty;
		private string _strSlots = "0";
		private string _strCost = string.Empty;

		private readonly Vehicle _vehicle;
	    private readonly Character _character;

		#region Constructor, Create, Save, Load, and Print Methods
		public WeaponMount(Character character, Vehicle vehicle)
		{
			// Create the GUID for the new VehicleMod.
			_guiID = Guid.NewGuid();
		    _character = character;
			_vehicle = vehicle;
        }

		/// Create a Vehicle Modification from an XmlNode and return the TreeNodes for it.
		/// <param name="objXmlMod">XmlNode to create the object from.</param>
		/// <param name="objNode">TreeNode to populate a TreeView.</param>
		/// <param name="objParent">Vehicle that the mod will be attached to.</param>
		/// <param name="intMarkup">Discount or markup that applies to the base cost of the mod.</param>
		public void Create(XmlNode objXmlMod, TreeNode objNode, Vehicle objParent, int intMarkup = 0)
		{
            Parent = objParent ?? _vehicle ?? throw new ArgumentNullException(nameof(objParent));
			if (objXmlMod == null) Utils.BreakIfDebug();
			objXmlMod.TryGetStringFieldQuickly("name", ref _strName);
			objXmlMod.TryGetStringFieldQuickly("category", ref _strCategory);
			objXmlMod.TryGetStringFieldQuickly("limit", ref _strLimit);
			objXmlMod.TryGetStringFieldQuickly("slots", ref _strSlots);
			objXmlMod.TryGetStringFieldQuickly("weaponmountcategories", ref _strWeaponMountCategories);
			objXmlMod.TryGetStringFieldQuickly("avail", ref _strAvail);

			// Check for a Variable Cost.
			if (objXmlMod["cost"] != null)
			{
				if (objXmlMod["cost"].InnerText.StartsWith("Variable"))
				{
					int intMin;
					var intMax = 0;
					char[] chrParentheses = { '(', ')' };
					string strCost = objXmlMod["cost"].InnerText.Replace("Variable", string.Empty).Trim(chrParentheses);
					if (strCost.Contains("-"))
					{
						string[] strValues = strCost.Split('-');
						intMin = Convert.ToInt32(strValues[0]);
						intMax = Convert.ToInt32(strValues[1]);
					}
					else
						intMin = Convert.ToInt32(strCost.Replace("+", string.Empty));

					if (intMin != 0 || intMax != 0)
					{
						var frmPickNumber = new frmSelectNumber();
						if (intMax == 0)
							intMax = 1000000;
						frmPickNumber.Minimum = intMin;
						frmPickNumber.Maximum = intMax;
						frmPickNumber.Description = LanguageManager.Instance.GetString("String_SelectVariableCost").Replace("{0}", DisplayNameShort);
						frmPickNumber.AllowCancel = false;
						frmPickNumber.ShowDialog();
						_strCost = frmPickNumber.SelectedValue.ToString();
					}
				}
				else
					_strCost = objXmlMod["cost"].InnerText;
			}
			_intMarkup = intMarkup;

			objXmlMod.TryGetStringFieldQuickly("source", ref _strSource);
			objXmlMod.TryGetStringFieldQuickly("page", ref _strPage);

			if (GlobalOptions.Instance.Language != "en-us")
			{
				XmlDocument objXmlDocument = XmlManager.Instance.Load("vehicles.xml");
				XmlNode objModNode = objXmlDocument.SelectSingleNode("/chummer/mods/mod[name = \"" + _strName + "\"]");
				if (objModNode != null)
				{
					objModNode.TryGetStringFieldQuickly("translate", ref _strAltName);
					objModNode.TryGetStringFieldQuickly("altpage", ref _strAltPage);
				}

				objModNode = objXmlDocument.SelectSingleNode("/chummer/categories/category[. = \"" + _strCategory + "\"]");
				_strAltCategory = objModNode?.Attributes?["translate"]?.InnerText;
			}

			objNode.Text = DisplayName;
			objNode.Tag = _guiID.ToString();
		}

		/// <summary>
		/// Save the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Save(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("mod");
			objWriter.WriteElementString("guid", _guiID.ToString());
			objWriter.WriteElementString("name", _strName);
			objWriter.WriteElementString("category", _strCategory);
			objWriter.WriteElementString("limit", _strLimit);
			objWriter.WriteElementString("slots", _strSlots);
			objWriter.WriteElementString("avail", _strAvail);
			objWriter.WriteElementString("cost", _strCost);
			objWriter.WriteElementString("markup", _intMarkup.ToString(CultureInfo.InvariantCulture));
			objWriter.WriteElementString("extra", _strExtra);
			objWriter.WriteElementString("source", _strSource);
			objWriter.WriteElementString("page", _strPage);
			objWriter.WriteElementString("included", _blnIncludeInVehicle.ToString());
			objWriter.WriteElementString("installed", _blnInstalled.ToString());
			objWriter.WriteElementString("weaponmountcategories", _strWeaponMountCategories);
			objWriter.WriteStartElement("weapons");
            foreach (Weapon w in _weapons)
            {
                w.Save(objWriter);
            }
            objWriter.WriteEndElement();
			objWriter.WriteElementString("notes", _strNotes);
			objWriter.WriteElementString("discountedcost", DiscountCost.ToString());
			objWriter.WriteEndElement();
			_character.SourceProcess(_strSource);
		}

		/// <summary>
		/// Load the VehicleMod from the XmlNode.
		/// </summary>
		/// <param name="objNode">XmlNode to load.</param>
		/// <param name="objVehicle">Vehicle that the mod is attached to.</param>
		/// <param name="blnCopy">Indicates whether a new item will be created as a copy of this one.</param>
		public void Load(XmlNode objNode, Vehicle objVehicle, bool blnCopy = false)
		{
			if (blnCopy)
			{
				_guiID = Guid.NewGuid();
			}
			else
			{
				objNode.TryGetField("guid", Guid.TryParse, out _guiID);
			}
			objNode.TryGetStringFieldQuickly("name", ref _strName);
			objNode.TryGetStringFieldQuickly("category", ref _strCategory);
			objNode.TryGetStringFieldQuickly("limit", ref _strLimit);
			objNode.TryGetStringFieldQuickly("slots", ref _strSlots);
			objNode.TryGetStringFieldQuickly("weaponmountcategories", ref _strWeaponMountCategories);
			objNode.TryGetStringFieldQuickly("page", ref _strPage);
			objNode.TryGetStringFieldQuickly("avail", ref _strAvail);
			objNode.TryGetStringFieldQuickly("cost", ref _strCost);
			objNode.TryGetInt32FieldQuickly("markup", ref _intMarkup);
			objNode.TryGetStringFieldQuickly("source", ref _strSource);
			objNode.TryGetBoolFieldQuickly("included", ref _blnIncludeInVehicle);
			objNode.TryGetBoolFieldQuickly("installed", ref _blnInstalled);
			if (objNode["weapons"] != null)
			{
                Weapon w = new Weapon(null);
                foreach (XmlNode n in objNode.SelectNodes("weapons/weapon"))
                {
                    w = new Weapon(null);
                    w.Load(objNode["weapon"], blnCopy);
                    _weapons.Add(w);
                }
			}
			objNode.TryGetStringFieldQuickly("notes", ref _strNotes);
			objNode.TryGetBoolFieldQuickly("discountedcost", ref _blnDiscountCost);
			objNode.TryGetStringFieldQuickly("extra", ref _strExtra);

			if (GlobalOptions.Instance.Language != "en-us")
			{
				XmlDocument objXmlDocument = XmlManager.Instance.Load("vehicles.xml");
				XmlNode objModNode = objXmlDocument.SelectSingleNode("/chummer/mods/mod[name = \"" + _strName + "\"]");
				if (objModNode != null)
				{
					objModNode.TryGetStringFieldQuickly("translate", ref _strAltName);
					objModNode.TryGetStringFieldQuickly("altpage", ref _strAltPage);
				}

				objModNode = objXmlDocument.SelectSingleNode("/chummer/categories/category[. = \"" + _strCategory + "\"]");
				_strAltCategory = objModNode?.Attributes?["translate"]?.InnerText;
			}
		}

		/// <summary>
		/// Print the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Print(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("mod");
			objWriter.WriteElementString("name", DisplayNameShort);
			objWriter.WriteElementString("category", DisplayCategory);
			objWriter.WriteElementString("limit", _strLimit);
			objWriter.WriteElementString("slots", _strSlots);
			objWriter.WriteElementString("avail", TotalAvail);
			objWriter.WriteElementString("cost", TotalCost.ToString());
			objWriter.WriteElementString("owncost", OwnCost.ToString());
			objWriter.WriteElementString("source", _character.Options.LanguageBookShort(_strSource));
			objWriter.WriteElementString("page", Page);
			objWriter.WriteElementString("included", _blnIncludeInVehicle.ToString());
            objWriter.WriteStartElement("weapons");
            foreach (Weapon w in _weapons)
            {
                w.Print(objWriter);
            }
            objWriter.WriteEndElement();
			if (_character.Options.PrintNotes)
				objWriter.WriteElementString("notes", _strNotes);
			objWriter.WriteEndElement();
		}
        /// <summary>
        /// Create a weapon mount using names instead of IDs, because user readability is important and untrustworthy. 
        /// </summary>
        /// <param name="objXmlVehicleMod"></param>
        internal void CreateByName(XmlNode objXmlVehicleMod)
        {
            XmlDocument doc = XmlManager.Instance.Load("vehicles.xml");
            //I'm not super pleased with this,
            TreeNode tree = new TreeNode();
            WeaponMount mount = this;
            XmlNode node = doc.SelectSingleNode($"/chummer/weaponmounts/weaponmount[name = \"{objXmlVehicleMod["size"].InnerText}\" and category = \"Size\"]");
            mount.Create(node, tree, _vehicle);
            WeaponMountOption option = new WeaponMountOption();
            node = doc.SelectSingleNode($"/chummer/weaponmounts/weaponmount[name = \"{objXmlVehicleMod["flexibility"].InnerText}\" and category = \"Flexibility\"]");
            option.Create(node["id"].InnerText);
            mount.WeaponMountOptions.Add(option);
            option = new WeaponMountOption();
            node = doc.SelectSingleNode($"/chummer/weaponmounts/weaponmount[name = \"{objXmlVehicleMod["control"].InnerText}\" and category = \"Control\"]");
            option.Create(node["id"].InnerText);
            mount.WeaponMountOptions.Add(option);
            option = new WeaponMountOption();
            node = doc.SelectSingleNode($"/chummer/weaponmounts/weaponmount[name = \"{objXmlVehicleMod["visibility"].InnerText}\" and category = \"Visibility\"]");
            option.Create(node["id"].InnerText);
            mount.WeaponMountOptions.Add(option);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Weapons.
        /// </summary>
        public List<Weapon> Weapons
		{
			get
			{
				return _weapons;
			}
		}

		/// <summary>
		/// Internal identifier which will be used to identify this piece of Gear in the Character.
		/// </summary>
		public string InternalId
		{
			get
			{
				return _guiID.ToString();
			}
		}

		/// <summary>
		/// Name.
		/// </summary>
		public string Name
		{
			get
			{
				return _strName;
			}
			set
			{
				_strName = value;
			}
		}

		/// <summary>
		/// Translated Category.
		/// </summary>
		public string DisplayCategory
		{
			get
			{
				if (!string.IsNullOrEmpty(_strAltCategory))
					return _strAltCategory;

				return _strCategory;
			}
		}

		/// <summary>
		/// Category.
		/// </summary>
		public string Category
		{
			get
			{
				return _strCategory;
			}
			set
			{
				_strCategory = value;
			}
		}

		/// <summary>
		/// Limits the Weapon Selection form to specified categories.
		/// </summary>
		public string WeaponMountCategories
		{
			set
			{
				_strWeaponMountCategories = value;
			}
			get
			{
				return _strWeaponMountCategories;
			}
		}

		/// <summary>
		/// Which Vehicle types the Mod is limited to.
		/// </summary>
		public string Limit
		{
			get
			{
				return _strLimit;
			}
			set
			{
				_strLimit = value;
			}
		}

		/// <summary>
		/// Number of Slots the Mod uses.
		/// </summary>
		public string Slots
		{
			get
			{
				return _strSlots;
			}
			set
			{
				_strSlots = value;
			}
		}

        /// <summary>
        /// Cost.
        /// </summary>
        public string Cost
		{
			get
			{
				return _strCost;
			}
			set
			{
				_strCost = value;
			}
		}

		/// <summary>
		/// Markup.
		/// </summary>
		public int Markup
		{
			get
			{
				return _intMarkup;
			}
			set
			{
				_intMarkup = value;
			}
		}

		/// <summary>
		/// Availability.
		/// </summary>
		public string Avail
		{
			get
			{
				return _strAvail;
			}
			set
			{
				_strAvail = value;
			}
		}

		/// <summary>
		/// Sourcebook.
		/// </summary>
		public string Source
		{
			get
			{
				return _strSource;
			}
			set
			{
				_strSource = value;
			}
		}

		/// <summary>
		/// Sourcebook Page Number.
		/// </summary>
		public string Page
		{
			get
			{
				if (!string.IsNullOrEmpty(_strAltPage))
					return _strAltPage;

				return _strPage;
			}
			set
			{
				_strPage = value;
			}
		}

		/// <summary>
		/// Whether or not the Mod included with the Vehicle by default.
		/// </summary>
		public bool IncludedInVehicle
		{
			get
			{
				return _blnIncludeInVehicle;
			}
			set
			{
				_blnIncludeInVehicle = value;
			}
		}

		/// <summary>
		/// Whether or not this Mod is installed and contributing towards the Vehicle's stats.
		/// </summary>
		public bool Installed
		{
			get
			{
				return _blnInstalled;
			}
			set
			{
				_blnInstalled = value;
			}
		}

		/// <summary>
		/// Notes.
		/// </summary>
		public string Notes
		{
			get
			{
				return _strNotes;
			}
			set
			{
				_strNotes = value;
			}
		}

		/// <summary>
		/// Value that was selected during an ImprovementManager dialogue.
		/// </summary>
		public string Extra
		{
			get
			{
				return _strExtra;
			}
			set
			{
				_strExtra = value;
			}
		}

		/// <summary>
		/// Whether or not the Vehicle Mod's cost should be discounted by 10% through the Black Market Pipeline Quality.
		/// </summary>
		public bool DiscountCost
		{
			get
			{
				return _blnDiscountCost;
			}
			set
			{
				_blnDiscountCost = value;
			}
		}

		/// <summary>
		/// Vehicle that the Mod is attached to. 
		/// </summary>
		public Vehicle Parent { internal get; set; }

        /// <summary>
        /// 
        /// </summary>
        public List<WeaponMountOption> WeaponMountOptions { get; set; } = new List<WeaponMountOption>();
        #endregion

        #region Complex Properties
        /// <summary>
        /// Total Availablility of the VehicleMod.
        /// </summary>
        public string TotalAvail
		{
			get
			{
				// If the Avail contains "+", return the base string and don't try to calculate anything since we're looking at a child component.
				if (_strAvail.StartsWith("+"))
					return _strAvail;

				string strCalculated = _strAvail;

				// Just a straight cost, so return the value.
				if (strCalculated.Contains("F") || strCalculated.Contains("R"))
				{
					strCalculated = Convert.ToInt32(strCalculated.Substring(0, strCalculated.Length - 1)) + strCalculated.Substring(strCalculated.Length - 1, 1);
				}
				else
					strCalculated = Convert.ToInt32(strCalculated).ToString();

				int intAvail;
				string strAvailText = string.Empty;
				if (strCalculated.Contains("F") || strCalculated.Contains("R"))
				{
					strAvailText = strCalculated.Substring(strCalculated.Length - 1);
					intAvail = Convert.ToInt32(strCalculated.Replace(strAvailText, string.Empty));
				}
				else
					intAvail = Convert.ToInt32(strCalculated);

				string strReturn = intAvail + strAvailText;

				// Translate the Avail string.
				strReturn = strReturn.Replace("R", LanguageManager.Instance.GetString("String_AvailRestricted"));
				strReturn = strReturn.Replace("F", LanguageManager.Instance.GetString("String_AvailForbidden"));

				return strReturn;
			}
		}

		/// <summary>
		/// Total cost of the VehicleMod.
		/// </summary>
		public int TotalCost
		{
			get
			{
				return OwnCost + Weapons.Sum(w => w.TotalCost);
			}
		}

		/// <summary>
		/// The cost of just the Vehicle Mod itself.
		/// </summary>
		public int OwnCost
		{
			get
			{
				// If the cost is determined by the Rating, evaluate the expression.
				int intReturn = Convert.ToInt32(_strCost);

				if (DiscountCost)
					intReturn = intReturn * 9 / 10;

				// Apply a markup if applicable.
				if (_intMarkup != 0)
				{
					double dblCost = Convert.ToDouble(intReturn, GlobalOptions.InvariantCultureInfo);
					dblCost *= 1 + (Convert.ToDouble(_intMarkup, GlobalOptions.InvariantCultureInfo) / 100.0);
					intReturn = Convert.ToInt32(dblCost);
				}

				return intReturn;
			}
		}

		/// <summary>
		/// The name of the object as it should be displayed on printouts (translated name only).
		/// </summary>
		public string DisplayNameShort
		{
			get
			{
				if (!string.IsNullOrEmpty(_strAltName))
					return _strAltName;

				return _strName;
			}
		}

		/// <summary>
		/// The name of the object as it should be displayed in lists. Qty Name (Rating) (Extra).
		/// </summary>
		public string DisplayName
		{
			get
			{
				string strReturn = DisplayNameShort;

				return strReturn;
			}
		}
        #endregion
    }

    public class WeaponMountOption
    {
        /// <summary>
        /// Category of the weapon mount.
        /// </summary>
        public string Category;

        private string _strAvail;
        private string _strName;
        private Guid _sourceID;
        private string _strCost;
        private string _strCategory;
        private string _strSlots;
        private string _strWeaponMountCategories;
        private int _intMarkup;
        private string _strAltName;
        private string _strAltPage;
        private string _strAltCategory;

        #region Constructor, Create, Save and Load Methods

        /// Create a Weapon Mount from an XmlNode.
        public void Create(string id)
        {
            XmlDocument xmlDoc = XmlManager.Instance.Load("vehicles.xml");
            XmlNode objXmlMod = xmlDoc.SelectSingleNode($"/chummer/weaponmounts/weaponmount[id = \"{id}\"]");
            if (objXmlMod == null) Utils.BreakIfDebug();
            objXmlMod.TryGetStringFieldQuickly("name", ref _strName);
            objXmlMod.TryGetStringFieldQuickly("category", ref _strCategory);
            objXmlMod.TryGetStringFieldQuickly("slots", ref _strSlots);
            objXmlMod.TryGetStringFieldQuickly("weaponcategories", ref _strWeaponMountCategories);
            objXmlMod.TryGetStringFieldQuickly("avail", ref _strAvail);

            // Check for a Variable Cost.
            // ReSharper disable once PossibleNullReferenceException
            if (objXmlMod["cost"] != null)
            {
                if (objXmlMod["cost"].InnerText.StartsWith("Variable"))
                {
                    int intMin;
                    var intMax = 0;
                    char[] chrParentheses = { '(', ')' };
                    string strCost = objXmlMod["cost"].InnerText.Replace("Variable", string.Empty).Trim(chrParentheses);
                    if (strCost.Contains("-"))
                    {
                        string[] strValues = strCost.Split('-');
                        intMin = Convert.ToInt32(strValues[0]);
                        intMax = Convert.ToInt32(strValues[1]);
                    }
                    else
                        intMin = Convert.ToInt32(strCost.Replace("+", string.Empty));

                    if (intMin != 0 || intMax != 0)
                    {
                        var frmPickNumber = new frmSelectNumber();
                        if (intMax == 0)
                            intMax = 1000000;
                        frmPickNumber.Minimum = intMin;
                        frmPickNumber.Maximum = intMax;
                        frmPickNumber.Description = LanguageManager.Instance.GetString("String_SelectVariableCost").Replace("{0}", DisplayName);
                        frmPickNumber.AllowCancel = false;
                        frmPickNumber.ShowDialog();
                        _strCost = frmPickNumber.SelectedValue.ToString();
                    }
                }
                else
                    _strCost = objXmlMod["cost"].InnerText;
            }
            if (GlobalOptions.Instance.Language == "en-us") return;
            XmlDocument objXmlDocument = XmlManager.Instance.Load("vehicles.xml");
            XmlNode objModNode = objXmlDocument.SelectSingleNode("/chummer/weaponmounts/weaponmount[id = \"" + _sourceID + "\"]");
            if (objModNode != null)
            {
                objModNode.TryGetStringFieldQuickly("translate", ref _strAltName);
                objModNode.TryGetStringFieldQuickly("altpage", ref _strAltPage);
            }

            objModNode = objXmlDocument.SelectSingleNode("/chummer/categories/category[. = \"" + _strCategory + "\"]");
            _strAltCategory = objModNode?.Attributes?["translate"]?.InnerText;
        }

        public string DisplayName { get; set; }

        /// <summary>
        /// Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("mod");
            objWriter.WriteElementString("id", _sourceID.ToString());
            objWriter.WriteElementString("name", _strName);
            objWriter.WriteElementString("category", _strCategory);
            objWriter.WriteElementString("slots", _strSlots);
            objWriter.WriteElementString("avail", _strAvail);
            objWriter.WriteElementString("cost", _strCost);
            objWriter.WriteEndElement();
        }

        /// <summary>
        /// Load the VehicleMod from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        /// <param name="objVehicle">Vehicle that the mod is attached to.</param>
        public void Load(XmlNode objNode, Vehicle objVehicle)
        {
            Guid.TryParse(objNode["id"].InnerText, out _sourceID);
            objNode.TryGetStringFieldQuickly("name", ref _strName);
            objNode.TryGetStringFieldQuickly("category", ref _strCategory);
            objNode.TryGetStringFieldQuickly("slots", ref _strSlots);
            objNode.TryGetStringFieldQuickly("weaponmountcategories", ref _strWeaponMountCategories);
            objNode.TryGetStringFieldQuickly("avail", ref _strAvail);
            objNode.TryGetStringFieldQuickly("cost", ref _strCost);
            objNode.TryGetInt32FieldQuickly("markup", ref _intMarkup);

            if (GlobalOptions.Instance.Language == "en-us") return;
            XmlDocument objXmlDocument = XmlManager.Instance.Load("vehicles.xml");
            XmlNode objModNode = objXmlDocument.SelectSingleNode($"/chummer/weaponmounts/weaponmount[id = \"{_sourceID}\"]");
            if (objModNode != null)
            {
                objModNode.TryGetStringFieldQuickly("translate", ref _strAltName);
                objModNode.TryGetStringFieldQuickly("altpage", ref _strAltPage);
            }

            objModNode = objXmlDocument.SelectSingleNode($"/chummer/categories/category[. = \"{_strCategory}\"]");
            _strAltCategory = objModNode?.Attributes?["translate"]?.InnerText;
        }
        #endregion
    }
}