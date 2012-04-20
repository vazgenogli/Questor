// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------

using System.Globalization;

namespace Questor.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DirectEve;
    using System.Xml.Linq;
    using System.IO;

    public class Arm
    {
        private bool _missionItemMoved;

        public Arm()
        {
            AmmoToLoad = new List<Ammo>();
        }

        // Bleh, we don't want this here, can we move it to cache?
        public long AgentId { get; set; }

        public ArmState State { get; set; }
        public List<Ammo> AmmoToLoad { get; private set; }

        public bool DefaultFittingChecked = false; //flag to check for the correct default fitting before using the fitting manager
        public bool DefaultFittingFound = true; //Did we find the default fitting?
        public bool TryMissionShip = true;  // Used in the event we can't find the ship specified in the missionfittings
        public bool UseMissionShip = false; // Were we successful in activating the mission specific ship?

        public void ProcessState()
        {
            // Select the correct ammo hangar

            switch (State)
            {
                case ArmState.Idle:
                    break;
                case ArmState.Done:
                    break;

                case ArmState.NotEnoughDrones:
                    //This is logged in questor.cs - do not double log
                    //Logging.Log("Arm: Armstate.NotEnoughDrones");
                    //State = ArmState.Idle;
                    break;

                case ArmState.NotEnoughAmmo:
                    //This is logged in questor.cs - do not double log
                    //Logging.Log("Arm: Armstate.NotEnoughAmmo");
                    //State = ArmState.Idle;
                    break;

                case ArmState.Begin:
                    //DefaultFittingChecked = false; //flag to check for the correct default fitting before using the fitting manager
                    //DefaultFittingFound = true; //Did we find the default fitting?
                    Cache.Instance.ArmLoadedCache = false;
                    TryMissionShip = true;  // Used in the event we can't find the ship specified in the missionfittings
                    UseMissionShip = false; // Were we successful in activating the mission specific ship?
                    State = ArmState.OpenShipHangar;
                    Cache.Instance.NextArmAction = DateTime.Now;
                    break;

                case ArmState.OpenShipHangar:
                case ArmState.SwitchToTransportShip:
                case ArmState.SwitchToSalvageShip:
                    if (DateTime.Now > Cache.Instance.NextArmAction) //default 10 seconds
                    {
                        if (!Cache.OpenShipsHangar("Arm")) break;

                        if (State == ArmState.OpenShipHangar)
                        {
                            Logging.Log("Arm: Activating combat ship");
                            State = ArmState.ActivateCombatShip;
                        }
                        else if (State == ArmState.SwitchToTransportShip)
                        {
                            Logging.Log("Arm: Activating transport ship");
                            State = ArmState.ActivateTransportShip;
                        }
                        else
                        {
                            Logging.Log("Arm: Activating salvage ship");
                            State = ArmState.ActivateSalvageShip;
                        }
                        break;
                    }
                    break;

                case ArmState.ActivateTransportShip:
                    if (DateTime.Now < Cache.Instance.NextArmAction) return;
                    string transportshipName = Settings.Instance.TransportShipName.ToLower();

                    if (string.IsNullOrEmpty(transportshipName))
                    {
                        State = ArmState.NotEnoughAmmo;
                        Logging.Log("Arm.ActivateTransportShip: Could not find transportshipName: " + transportshipName + " in settings!");
                        return;
                    }
                    if (Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() != transportshipName)
                    {
                        List<DirectItem> ships = Cache.Instance.DirectEve.GetShipHangar().Items;
                        foreach (DirectItem ship in ships.Where(ship => ship.GivenName != null && ship.GivenName.ToLower() == transportshipName))
                        {
                            Logging.Log("Arm: Making [" + ship.GivenName + "] active");
                            ship.ActivateShip();
                            Cache.Instance.NextArmAction = DateTime.Now.AddSeconds((int)Time.SwitchShipsDelay_seconds);
                        }
                        return;
                    }
                    if (DateTime.Now > Cache.Instance.NextArmAction) //default 7 seconds
                    {
                        if (Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() == transportshipName)
                        {
                            Logging.Log("Arm.ActivateTransportShip: Done");
                            State = ArmState.Done;
                            return;
                        }
                    }
                    break;

                case ArmState.ActivateSalvageShip:
                    string salvageshipName = Settings.Instance.SalvageShipName.ToLower();

                    if (DateTime.Now > Cache.Instance.NextArmAction) //default 10 seconds
                    {
                        if (string.IsNullOrEmpty(salvageshipName))
                        {
                            State = ArmState.NotEnoughAmmo;
                            Logging.Log("Arm.ActivateSalvageShip: Could not find salvageshipName: " + salvageshipName + " in settings!");
                            return;
                        }

                        if ((!string.IsNullOrEmpty(salvageshipName) && Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() != salvageshipName))
                        {
                            if (DateTime.Now > Cache.Instance.NextArmAction)
                            {
                                List<DirectItem> ships = Cache.Instance.DirectEve.GetShipHangar().Items;
                                foreach (DirectItem ship in ships.Where(ship => ship.GivenName != null && ship.GivenName.ToLower() == salvageshipName))
                                {
                                    Logging.Log("Arm: Making [" + ship.GivenName + "] active");
                                    ship.ActivateShip();
                                    Cache.Instance.NextArmAction = DateTime.Now.AddSeconds((int)Time.SwitchShipsDelay_seconds);
                                }
                                return;
                            }
                            return;
                        }
                        if (DateTime.Now > Cache.Instance.NextArmAction && (!string.IsNullOrEmpty(salvageshipName) && Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() != salvageshipName))
                        {
                            State = ArmState.OpenShipHangar;
                            break;
                        }
                        if (DateTime.Now > Cache.Instance.NextArmAction)
                        {
                            Logging.Log("Arm: Done");
                            State = ArmState.Done;
                            return;
                        }
                    }
                    break;

                case ArmState.ActivateCombatShip:
                    string shipName = Settings.Instance.CombatShipName.ToLower();

                    if (DateTime.Now < Cache.Instance.NextArmAction) return;//default is 3 seconds after opening items hangar
                    {
                        if (string.IsNullOrEmpty(shipName))
                        {
                            State = ArmState.NotEnoughAmmo;
                            Logging.Log("Arm.ActivateCombatShip: Could not find CombatShipName: " + shipName + " in settings!");
                            return;
                        }
                        if (!Cache.Instance.ArmLoadedCache)
                        {
                            _missionItemMoved = false;
                            Cache.Instance.RefreshMissionItems(AgentId);
                            Cache.Instance.ArmLoadedCache = true;
                        }
                        // If we've got a mission-specific ship defined, switch to it
                        if ((State == ArmState.ActivateCombatShip) && !string.IsNullOrEmpty(Cache.Instance.MissionShip) && TryMissionShip)
                            shipName = Cache.Instance.MissionShip.ToLower();

                        if (Settings.Instance.CombatShipName.ToLower() == shipName) // if the mission specific ship is our default combat ship, no need to do anything special
                            TryMissionShip = false;

                        if ((!string.IsNullOrEmpty(shipName) && Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() != shipName))
                        {
                            if (DateTime.Now > Cache.Instance.NextArmAction)
                            {
                                List<DirectItem> ships = Cache.Instance.DirectEve.GetShipHangar().Items;
                                foreach (DirectItem ship in ships.Where(ship => ship.GivenName != null && ship.GivenName.ToLower() == shipName))
                                {
                                    Logging.Log("Arm: Making [" + ship.GivenName + "] active");
                                    ship.ActivateShip();
                                    Cache.Instance.NextArmAction = DateTime.Now.AddSeconds((int)Time.SwitchShipsDelay_seconds);
                                    if (TryMissionShip)
                                        UseMissionShip = true;
                                    return;
                                }

                                if (TryMissionShip && !UseMissionShip)
                                {
                                    Logging.Log("Arm: Unable to find the ship specified in the missionfitting.  Using default combat ship and default fitting.");
                                    TryMissionShip = false;
                                    Cache.Instance.Fitting = Cache.Instance.DefaultFitting;
                                    return;
                                }

                                State = ArmState.NotEnoughAmmo;
                                Logging.Log("Arm: Found the following ships:");
                                foreach (DirectItem ship in ships)
                                {
                                    Logging.Log("Arm: [" + ship.GivenName + "]");
                                }
                                Logging.Log("Arm: Could not find [" + shipName + "] ship!");
                                return;
                            }
                            return;
                        }

                        if ((!string.IsNullOrEmpty(shipName) && Cache.Instance.DirectEve.ActiveShip.GivenName.ToLower() != shipName))
                        {
                            State = ArmState.OpenShipHangar;
                            break;
                        }
                        if (TryMissionShip)
                            UseMissionShip = true;

                        //if (State == ArmState.ActivateSalvageShip)
                        //{
                        //    Logging.Log("Arm: Done");
                        //    State = ArmState.Done;
                        //    return;
                        //}

                        //_missionItemMoved = false;
                        //Cache.Instance.RefreshMissionItems(AgentId);
                        if (AmmoToLoad.Count == 0 && string.IsNullOrEmpty(Cache.Instance.BringMissionItem))
                        {
                            Logging.Log("Arm: Done");
                            State = ArmState.Done;
                        }
                        else
                        {
                            State = ArmState.OpenAmmoHangar;
                        }
                    }
                    break;

                case ArmState.OpenAmmoHangar:
                    // Is the hangar open?

                    if (!Cache.OpenAmmoHangar("Arm")) break;

                    Logging.Log("Arm: Done Opening Item hangar: proceeding to Cargo Hold");
                    State = ArmState.OpenCargo;
                    break;

                case ArmState.OpenCargo:
                    // Is cargo open?
                    if (!Cache.OpenCargoHold("Arm")) break;

                    if (Settings.Instance.UseDrones && (Cache.Instance.DirectEve.ActiveShip.GroupId != 31 && Cache.Instance.DirectEve.ActiveShip.GroupId != 28 && Cache.Instance.DirectEve.ActiveShip.GroupId != 380))
                    {
                        Logging.Log("Arm: Opening ship's drone bay");
                        State = ArmState.OpenDroneBay;
                    }
                    else if ((Settings.Instance.UseFittingManager && DefaultFittingFound) && !(UseMissionShip && !(Cache.Instance.ChangeMissionShipFittings)))
                    {
                        State = ArmState.OpenFittingWindow;
                    }
                    else
                        State = ArmState.MoveItems;
                    break;

                case ArmState.OpenFittingWindow:
                    //let's check first if we need to change fitting at all
                    Logging.Log("Arm: Fitting: " + Cache.Instance.Fitting + " - currentFit: " + Cache.Instance.CurrentFit);
                    if (Cache.Instance.Fitting.Equals(Cache.Instance.CurrentFit))
                    {
                        Logging.Log("Arm: Current fit is correct - no change necessary");
                        State = ArmState.MoveItems;
                    }
                    else
                    {
                        if (DateTime.Now > Cache.Instance.NextArmAction)
                        {
                            Cache.Instance.DirectEve.OpenFitingManager(); //you should only have to issue this command once
                            Cache.Instance.NextArmAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber3To7());
                            Logging.Log("Arm: Opening Fitting Manager: waiting [" + Math.Round(Cache.Instance.NextArmAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]");
                            State = ArmState.WaitForFittingWindow;
                        }
                    }
                    break;

                case ArmState.WaitForFittingWindow:
                    DirectFittingManagerWindow fittingMgr = Cache.Instance.DirectEve.Windows.OfType<DirectFittingManagerWindow>().FirstOrDefault();
                    if (DateTime.Now < Cache.Instance.NextArmAction) return;
                    {
                        //open it again ?
                        if (fittingMgr == null)
                        {
                            Cache.Instance.DirectEve.OpenFitingManager(); //you should only have to issue this command once
                            Cache.Instance.NextArmAction = DateTime.Now.AddSeconds(Settings.Instance.RandomNumber5To10());
                            Logging.Log("Arm: Opening fitting manager: waiting [" + Math.Round(Cache.Instance.NextArmAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]");
                        }
                    }
                    if (fittingMgr != null && (fittingMgr.IsReady)) //check if it's ready
                    {
                        State = ArmState.ChoseFitting;
                    }
                    break;

                case ArmState.ChoseFitting:
                    fittingMgr = Cache.Instance.DirectEve.Windows.OfType<DirectFittingManagerWindow>().FirstOrDefault();
                    bool found = false;
                    if (!DefaultFittingChecked)
                    {
                        DefaultFittingChecked = true;
                        Logging.Log("Arm: Looking for Default Fitting " + Cache.Instance.DefaultFitting);
                        if (fittingMgr != null)
                        {
                            foreach (DirectFitting fitting in fittingMgr.Fittings)
                            {
                                //ok found it
                                if (Cache.Instance.DefaultFitting.ToLower().Equals(fitting.Name.ToLower()))
                                {
                                    found = true;
                                    Logging.Log("Arm: Found Default Fitting " + fitting.Name);
                                }
                            }
                            if (!found)
                            {
                                Logging.Log("Arm: Error! Couldn't find Default Fitting.  Disabling fitting manager.");
                                DefaultFittingFound = false;
                                Settings.Instance.UseFittingManager = false;
                                State = ArmState.MoveItems;
                                break;
                            }
                            found = false;
                        }
                        else
                        {
                            return;
                        }
                    }
                    Logging.Log("Arm: Looking for fitting " + Cache.Instance.Fitting);
                    if (DateTime.Now > Cache.Instance.NextArmAction)
                    {
                        if (fittingMgr != null)
                        {
                            foreach (DirectFitting fitting in fittingMgr.Fittings)
                            {
                                //ok found it
                                DirectActiveShip ship = Cache.Instance.DirectEve.ActiveShip;
                                if (Cache.Instance.Fitting.ToLower().Equals(fitting.Name.ToLower()) && fitting.ShipTypeId == ship.TypeId)
                                {
                                    Cache.Instance.NextArmAction = DateTime.Now.AddSeconds((int)Time.SwitchShipsDelay_seconds);
                                    Logging.Log("Arm: Found fitting [ " + fitting.Name + " ][" + Math.Round(Cache.Instance.NextArmAction.Subtract(DateTime.Now).TotalSeconds, 0) + "sec]");
                                    //switch to the requested fitting for the current mission
                                    fitting.Fit();
                                    Cache.Instance.CurrentFit = fitting.Name;
                                    State = ArmState.WaitForFitting;
                                    found = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                        //if we didn't find it, we'll set currentfit to default
                        //this should provide backwards compatibility without trying to fit always
                        if (!found)
                        {
                            if (UseMissionShip)
                            {
                                Logging.Log("Arm: Couldn't find fitting for this ship typeid.  Using current fitting.");
                                State = ArmState.MoveItems;
                                break;
                            }
                            else
                            {
                                Logging.Log("Arm: Couldn't find fitting - switching to default");
                                Cache.Instance.Fitting = Cache.Instance.DefaultFitting;
                                break;
                            }
                        }
                        State = ArmState.MoveItems;
                        Logging.Log("Arm: Closing Fitting Manager");
                        fittingMgr.Close();
                    }
                    break;

                case ArmState.WaitForFitting:
                    //let's wait 10 seconds
                    if (DateTime.Now > Cache.Instance.NextArmAction && Cache.Instance.DirectEve.GetLockedItems().Count == 0)
                    {
                        //we should be done fitting, proceed to the next state
                        State = ArmState.MoveItems;
                        fittingMgr = Cache.Instance.DirectEve.Windows.OfType<DirectFittingManagerWindow>().FirstOrDefault();
                        if (fittingMgr != null) fittingMgr.Close();
                        Cache.Instance.NextArmAction = DateTime.Now.AddSeconds((int)Time.FittingWindowLoadFittingDelay_seconds);
                        Logging.Log("Arm: Done fitting");
                    }
                    else Logging.Log("Arm: Waiting for fitting. locked items = " + Cache.Instance.DirectEve.GetLockedItems().Count);
                    break;

                case ArmState.OpenDroneBay:
                    // Is the drone bay open?
                    if (!Cache.OpenDroneBay("Arm")) break;

                    Logging.Log("Arm: Moving drones");
                    State = ArmState.MoveDrones;
                    break;

                case ArmState.MoveDrones:
                    if (!Cache.OpenShipsHangar("Arm")) break;
                    
                    if (!Cache.OpenDroneBay("Arm")) break;
                    
                    if (!Cache.OpenAmmoHangar("Arm")) break;
                    
                    
                    DirectItem drone;
                    drone = Cache.Instance.AmmoHangar.Items.FirstOrDefault(i => i.TypeId == Settings.Instance.DroneTypeId);
                    if (drone == null || drone.Stacksize < 1)
                    {
                        Logging.Log("Arm: Out of drones in [" + Settings.Instance.AmmoHangar + "]");
                        State = ArmState.NotEnoughDrones;
                        break;
                    }

                    double neededDrones = Math.Floor((Cache.Instance.DroneBay.Capacity - Cache.Instance.DroneBay.UsedCapacity) / drone.Volume);
                    Logging.Log("Arm: neededDrones: " + neededDrones);
                    if (neededDrones == 0 && ((Settings.Instance.UseFittingManager && DefaultFittingFound) && !(UseMissionShip && !(Cache.Instance.ChangeMissionShipFittings))))
                    {
                        Logging.Log("Arm: Fitting");
                        State = ArmState.OpenFittingWindow;
                        break;
                    }

                    if (neededDrones == 0)
                    {
                        State = ArmState.MoveItems;
                        break;
                    }

                    // Move needed drones
                    Logging.Log("Arm: Move [ " + (int)Math.Min(neededDrones, drone.Stacksize) + " ] Drones into drone bay");
                    Cache.Instance.DroneBay.Add(drone, (int)Math.Min(neededDrones, drone.Stacksize));
                    break;

                case ArmState.MoveItems:
                    if (!Cache.OpenCargoHold("Arm")) break;
  
                    if (!Cache.OpenAmmoHangar("Arm")) break;
                        
                    
                    string bringItem = Cache.Instance.BringMissionItem;
                    if (string.IsNullOrEmpty(bringItem))
                        _missionItemMoved = true;

                    if (!_missionItemMoved)
                    {
                        if (!Cache.OpenAmmoHangar("Arm")) break;
                        DirectItem missionItem = Cache.Instance.AmmoHangar.Items.FirstOrDefault(i => (i.TypeName ?? string.Empty).ToLower() == bringItem);
                        if (missionItem == null)
                           missionItem = Cache.Instance.AmmoHangar.Items.FirstOrDefault(i => (i.TypeName ?? string.Empty).ToLower() == bringItem);

                        if (missionItem != null && !string.IsNullOrEmpty(missionItem.TypeName.ToString(CultureInfo.InvariantCulture)))
                        {
                            Logging.Log("Arm: Moving MissionItem [" + missionItem.TypeName + "] to CargoHold");

                            Cache.Instance.CargoHold.Add(missionItem, 1);
                            _missionItemMoved = true;
                            break;
                        }
                    }

                    bool itemMoved = false;
                    if (Cache.Instance.MissionAmmo.Count() != 0)
                    {
                        AmmoToLoad = new List<Ammo>(Cache.Instance.MissionAmmo);
                    }
                    foreach (DirectItem item in Cache.Instance.AmmoHangar.Items.OrderBy(i => i.Quantity))
                    {
                        if (item.ItemId <= 0)
                            continue;

                        Ammo ammo = AmmoToLoad.FirstOrDefault(a => a.TypeId == item.TypeId);
                        if (ammo == null)
                            continue;

                        Logging.Log("Arm: Moving Ammo [" + item.TypeName + "] to CargoHold");

                        int moveQuantity = Math.Min(item.Quantity, ammo.Quantity);
                        moveQuantity = Math.Max(moveQuantity, 1);
                        Cache.Instance.CargoHold.Add(item, moveQuantity);

                        ammo.Quantity -= moveQuantity;
                        if (ammo.Quantity <= 0)
                        {
                            Cache.Instance.MissionAmmo.RemoveAll(a => a.TypeId == item.TypeId);
                            AmmoToLoad.RemoveAll(a => a.TypeId == item.TypeId);
                        }
                        itemMoved = true;
                        break;
                    }

                    if (AmmoToLoad.Count == 0 && _missionItemMoved)
                    {
                        Cache.Instance.NextArmAction = DateTime.Now.AddSeconds((int)Time.WaitforItemstoMove_seconds);

                        Logging.Log("Arm: Waiting for items");
                        State = ArmState.WaitForItems;
                    }
                    else if (!itemMoved)
                    {
                        if (AmmoToLoad.Count > 0)
                            foreach (Ammo ammo in AmmoToLoad)
                            {
                                Logging.Log("Arm: Missing ammo with TypeId [" + ammo.TypeId + "]");
                            }

                        if (!_missionItemMoved)
                            Logging.Log("Arm: Missing mission item [" + bringItem + "]");

                        State = ArmState.NotEnoughAmmo;
                    }
                    break;

                case ArmState.WaitForItems:
                    // Wait 5 seconds after moving
                    if (DateTime.Now < Cache.Instance.NextArmAction)
                        break;

                    if (!Cache.OpenCargoHold("Arm")) break;
                    
                    if (Cache.Instance.CargoHold.Items.Count == 0)
                        break;

                    if (Cache.Instance.DirectEve.GetLockedItems().Count == 0)
                    {
                        // Close the drone bay, its not required in space.
                        if (Cache.Instance.DroneBay.IsReady)
                           Cache.Instance.DroneBay.Window.Close();

                        Logging.Log("Arm: Done");

                        //reload the ammo setting for combat
                        try
                        {
                            DirectAgentMission mission = Cache.Instance.DirectEve.AgentMissions.FirstOrDefault(m => m.AgentId == AgentId);
                            if (mission == null)
                                return;

                            string missionName = Cache.Instance.FilterPath(mission.Name);
                            string missionXmlPath = Path.Combine(Settings.Instance.MissionsPath, missionName + ".xml");
                            XDocument missionXml = XDocument.Load(missionXmlPath);
                            Cache.Instance.MissionAmmo = new List<Ammo>();
                            if (missionXml.Root != null)
                            {
                                XElement ammoTypes = missionXml.Root.Element("missionammo");
                                if (ammoTypes != null)
                                    foreach (XElement ammo in ammoTypes.Elements("ammo"))
                                        Cache.Instance.MissionAmmo.Add(new Ammo(ammo));
                            }
                        }
                        catch (Exception e)
                        {
                            Logging.Log("Arms.WaitForItems: Unable to load missionammo from mission XML: no XML found, " + e.Message);
                            Cache.Instance.MissionAmmo = new List<Ammo>();
                        }

                        State = ArmState.Done;
                        break;
                    }

                    // Note, there's no unlock here as we *always* want our ammo!
                    break;
            }
        }
    }
}