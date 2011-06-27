﻿using System;
using System.Collections.Generic;
using OpenDMS.IO;

namespace OpenDMS.Storage.Providers.CouchDB.Transactions
{
    public class Transaction
    {
        public Directory Directory { get; private set; }

        public Transaction(Directory directory)
        {
            Directory = directory;
        }

        public void Begin(string username, TimeSpan expiryDuration)
        {
            Lock loc;
            File file;

            try
            {
                // Does this transaction already exist?  Has it expired?
                if (Directory.Exists())
                {
                    file = new File(Directory, "lock");

                    // Does the lock exist?
                    if (file.Exists())
                    {
                        string errorMessage;
                        // Yes - load it
                        loc = Lock.Load(file);

                        // If the user can access then we just clear it out and relock
                        if (!loc.CanUserAccess(username, out errorMessage))
                            throw new ActiveTransactionException(errorMessage);
                        else
                        {
                            Reset();
                            Directory.Create();
                            loc.ResetExpiry();
                            loc.WriteLock(file);
                        }
                    }
                    else
                    {
                        // Nope - clear out this transaction and create the lock
                        Reset();
                        Directory.Create();
                        loc = new Lock(username, expiryDuration);
                        loc.WriteLock(file);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to begin the transaction.", e);
                throw;
            }
        }

        public Stage GetStage(string username, int stageNumber)
        {
            Stage stage;
            Lock loc;

            try
            {
                loc = GetLockAndCheckAccess(username);

                stage = new Stage(this, stageNumber);
                if (!stage.Exists())
                    throw new InvalidStageException("The stage does not exist.");
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to get the stage of the transaction.", e);
                throw;
            }

            return stage;
        }

        public bool TryGetStage(string username, int stageNumber, out Stage stage)
        {
            Lock loc;

            try
            {
                loc = GetLockAndCheckAccess(username);

                stage = new Stage(this, stageNumber);

                if (!stage.Exists())
                {
                    stage = null;
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to get the stage of the transaction.", e);
                throw;
            }

            return true;
        }

        public Stage GetCurrentStage(string username)
        {
            Lock loc;
            int stageNum;

            try
            {
                loc = GetLockAndCheckAccess(username);

                if ((stageNum = GetCurrentStageNumber()) < 0)
                    throw new InvalidTransactionException("A transaction does not exist.");
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to get the stage of the transaction.", e);
                throw;
            }

            // highNum now contains the # of the current stage.
            return new Stage(this, stageNum);
        }

        public Stage AdvanceStage(string username)
        {
            Lock loc;
            int currentStateNumber = -1;

            try
            {
                loc = GetLockAndCheckAccess(username);

                if ((currentStateNumber = GetCurrentStageNumber()) < 0)
                    throw new InvalidTransactionException("A transaction does not exist.");
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to advance the stage of the transaction.", e);
                throw;
            }
            
            // highNum now contains the # of the current stage.
            return new Stage(this, currentStateNumber + 1);
        }

        public Stage Undo(string username, int quantityOfSteps)
        {
            Lock loc;
            int currentStageNumber = -1;
            int currentMinusSteps = -1;
            List<Directory> directories;
            int temp;
            Stage stage;

            try
            {
                loc = GetLockAndCheckAccess(username);

                directories = Directory.GetDirectories();

                currentStageNumber = GetCurrentStageNumber();

                currentMinusSteps = currentStageNumber - quantityOfSteps;

                // Eliminate anything higher than currentMinusSteps
                for (int i = 0; i < directories.Count; i++)
                {
                    temp = int.Parse(directories[i].GetDirectoryShortName());
                    if (temp > currentMinusSteps)
                    {
                        stage = new Stage(this, temp);
                        stage.Delete();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to undo stage(s) of the transaction.", e);
                throw;
            }

            return new Stage(this, currentMinusSteps);
        }

        public void Abort(string username)
        {
            try
            {
                Lock loc = GetLockAndCheckAccess(username);
                if (loc != null)
                    Reset();
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to abort the transaction.", e);
                throw;
            }
        }

        public bool Commit(string username, Actions.Base action, out string errorMessage)
        {
            Stage stage;

            try
            {
                stage = GetCurrentStage(username);

                if (!stage.Commit(action, out errorMessage))
                    return false;

                Reset();
                return true;
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to commit the transaction.", e);
                throw;
            }
        }

        private int GetCurrentStageNumber()
        {
            List<Directory> directories;
            int highNum = 0, temp;

            try
            {
                if (!Directory.Exists())
                    return -1;

                directories = Directory.GetDirectories();

                for (int i = 0; i < directories.Count; i++)
                {
                    temp = int.Parse(directories[i].GetDirectoryShortName());
                    if (temp > highNum)
                        highNum = temp;
                }
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to get the current stage number of the transaction.", e);
                throw;
            }

            return highNum;
        }

        private Lock GetLockAndCheckAccess(string username)
        {
            Lock loc;
            string errorMessage;

            try
            {
                loc = GetLock();

                if (!loc.CanUserAccess(username, out errorMessage))
                    throw new ActiveTransactionException(errorMessage);
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to get the lock and check the access rights of the transaction.", e);
                throw;
            }

            return loc;
        }

        internal Lock GetLock()
        {
            File file;

            try
            {
                // Does this transaction already exist?  Has it expired?
                if (Directory.Exists())
                {
                    file = new File(Directory, "lock");

                    // Does the lock exist?
                    if (!file.Exists())
                        throw new InvalidTransactionException("A transaction lock does not exist.");

                    return Lock.Load(file);
                }
                else
                    throw new InvalidTransactionException("The transaction does not exist.");
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to get the lock of the transaction.", e);
                throw;
            }
        }

        public void UpdateLockExpiry()
        {
            File file;
            Lock loc;

            try
            {
                file = new File(Directory, "lock");

                // Does the lock exist?
                if (!file.Exists())
                    throw new InvalidTransactionException("A transaction lock does not exist.");

                loc = Lock.Load(file);
                loc.ResetExpiry();
                loc.WriteLock(file);
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to update the lock expiration of the transaction.", e);
                throw;
            }
        }

        private void Reset()
        {
            try
            {
                if (Directory.Exists())
                {
                    Directory.Delete();
                }
            }
            catch (Exception e)
            {
                Logger.Storage.Error("An exception occurred while trying to reset of the transaction.", e);
                throw;
            }
        }
    }
}
