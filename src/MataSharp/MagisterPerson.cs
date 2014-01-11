﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MataSharp
{
    public partial class MagisterPerson
    {
        public uint ID { get; set; }
        public object Ref { get; set; } // Even Schoolmaster doesn't know what this is, it's mysterious. Just keep it in case.
        public string Initials { get; set; }
        public string SurName { get; set; }
        public string FirstName { get; set; }
        public string NamePrefix { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Group { get; set; }
        public string TeacherCode { get; set; }
        public int GroupID { get; set; }

        /// <summary>
        /// Returns all Magisterpersons filterd by the given search filter as a list.
        /// </summary>
        /// <param name="SearchFilter">The search filter to use as string.</param>
        /// <returns>List containing MagisterPerson instances</returns>
        public static List<MagisterPerson> GetPersons(string SearchFilter)
        {
            if (string.IsNullOrWhiteSpace(SearchFilter) || SearchFilter.Count() < 3) return new List<MagisterPerson>();

            string URL = "https://" + _Session.School.URL + "/api/personen/" + _Session.Mata.UserID + "/communicatie/contactpersonen?q=" + SearchFilter;

            string personsRAW = _Session.HttpClient.client.DownloadString(URL);
            return JArray.Parse(personsRAW).ToList().ConvertAll(p => p.ToObject<MagisterStylePerson>().ToPerson());
        }

        public bool Equals(MagisterPerson Person)
        {
            if (Person != null && this.ID == Person.ID && this.Group == Person.Group && this.GroupID == Person.GroupID)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Converts the current MagisterPerson instance to a MagisterStylePerson
        /// </summary>
        /// <returns>A MagisterStylePerson instance.</returns>
        internal MagisterStylePerson ToMagisterStyle()
        {
            var tmpPerson = (GetPersons(this.Name).Count == 1) ? GetPersons(this.Name)[0] : this; //Takes the person from the server, if it are more or less than one, use this instance instead.
            return new MagisterStylePerson()
                {
                    Id = tmpPerson.ID,
                    Ref = tmpPerson.Ref,
                    Achternaam = tmpPerson.SurName,
                    Voornaam = tmpPerson.FirstName,
                    Tussenvoegsel = tmpPerson.NamePrefix,
                    Naam = tmpPerson.Name,
                    Omschrijving = tmpPerson.Description,
                    Groep = tmpPerson.Group,
                    DocentCode = tmpPerson.TeacherCode,
                    Type = tmpPerson.GroupID
                };
        }
    }
    internal partial struct MagisterStylePerson
    {
        public uint Id { get; set; }
        public object Ref { get; set; } // Even Schoolmaster doesn't know what this is, it's mysterious. Just keep it in case.
        public string Achternaam { get; set; }
        public string Voornaam { get; set; }
        public string Tussenvoegsel { get; set; }
        public string Naam { get; set; }
        public string Omschrijving { get; set; }
        public string Groep { get; set; }
        public string DocentCode { get; set; }
        public int Type { get; set; }
        public string Voornamen { get; set; }
        public string Voorletters { get; set; }

        private static List<MagisterStylePerson> GetPersons(string SearchFilter)
        {
            if (string.IsNullOrWhiteSpace(SearchFilter) || SearchFilter.Count() < 3) return new List<MagisterStylePerson>();

            string URL = "https://" + _Session.School.URL + "/api/personen/" + _Session.Mata.UserID + "/communicatie/contactpersonen?q=" + SearchFilter;

            string personsRAW = _Session.HttpClient.client.DownloadString(URL);
            return JArray.Parse(personsRAW).ToList().ConvertAll(p => p.ToObject<MagisterStylePerson>());
        }

        public MagisterPerson ToPerson()
        {
            MagisterStylePerson tmpPerson;
            try { tmpPerson = (MagisterStylePerson.GetPersons(this.Naam).Count == 1) ? MagisterStylePerson.GetPersons(this.Naam)[0] : this; } //Main building ground.
            catch { tmpPerson = this; }

            List<string> splitted = (tmpPerson.Naam != null) ? tmpPerson.Naam.Split(' ').ToList() : null;

            string tmpFirstName = (tmpPerson.Voornaam == null && splitted != null) ? splitted[0] : tmpPerson.Voornaam + tmpPerson.Voornamen;
            string tmpSurName = (tmpPerson.Achternaam == null && splitted != null) ? splitted[splitted.Count - 1] : tmpPerson.Achternaam;
            string tmpPrefix = tmpPerson.Tussenvoegsel;

            if (tmpPrefix == null)
            {
                try
                {
                    splitted.RemoveAt(0); splitted.RemoveAt(splitted.Count - 1);

                    if (splitted != null && splitted.Count != 0 && splitted[0] != "")
                        tmpPrefix = string.Join(" ", splitted);
                }
                catch {/*When there isn't a prefix*/}
            }

            string tmpName = (tmpPerson.Naam == null && tmpFirstName != null && tmpSurName != null && tmpPrefix != null) ? 
                string.Concat(tmpFirstName, " ", tmpPrefix, " ", tmpSurName) :
                (tmpPerson.Naam == null && tmpFirstName != null && tmpSurName != null) ? string.Concat(tmpFirstName, " ", tmpSurName) : 
                tmpPerson.Naam;
            //This all above ISN'T needed but makes everything a bit nicer :)

            return new MagisterPerson()
            {
                ID = tmpPerson.Id,
                Ref = tmpPerson.Ref,
                SurName = tmpSurName,
                FirstName = tmpFirstName,
                NamePrefix = tmpPrefix,
                Name = tmpName,
                Description = tmpPerson.Omschrijving,
                Group = tmpPerson.Groep,
                TeacherCode = tmpPerson.DocentCode,
                GroupID = tmpPerson.Type,
                Initials = tmpPerson.Voorletters,
            };
        }
    }
}