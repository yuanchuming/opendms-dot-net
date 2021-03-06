﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OpenDMS.Storage.Providers.CouchDB.Transitions
{
    public class User
    {
        public User()
        {
        }

        public Security.User Transition(Model.Document document)
        {
            List<string> groups = null;

            try
            {
                if (document["Groups"] != null)
                {
                    groups = new List<string>();
                    JArray jarray = (JArray)document["Groups"];

                    for (int i = 0; i < jarray.Count; i++)
                    {
                        string groupName = jarray[i].Value<string>();
                        if (groupName.StartsWith("group-"))
                            groupName = groupName.Substring(6);
                        groups.Add(groupName);
                    }
                }

                Security.User user = new Security.User(document.Id,
                    document.Rev,
                    null,
                    document["FirstName"].Value<string>(),
                    document["MiddleName"].Value<string>(),
                    document["LastName"].Value<string>(),
                    groups,
                    document["Superuser"].Value<bool>());
                user.SetEncryptedPassword(document["Password"].Value<string>());
                return user;
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while attempting to parse the document.", e);
                throw;
            }
        }

        public Model.Document Transition(Security.User user)
        {
            Model.Document doc = new Model.Document();
            JArray jarray = null;

            try
            {
                doc.Id = user.Id;
                if (user.Rev != null)
                    doc.Rev = user.Rev;
                doc["Type"] = "user";
                doc["Password"] = user.Password;
                doc["FirstName"] = user.FirstName;
                doc["MiddleName"] = user.MiddleName;
                doc["LastName"] = user.LastName;
                doc["Superuser"] = user.IsSuperuser;

                if (user.Groups != null)
                {
                    jarray = new JArray();
                    for (int i = 0; i < user.Groups.Count; i++)
                        jarray.Add(user.Groups[i]);
                }

                doc["Groups"] = jarray;
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while attempting to parse the user.", e);
                throw;
            }

            return doc;
        }
    }
}
