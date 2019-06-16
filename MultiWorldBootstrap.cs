/*
 * MIT License
 * 
 * Copyright (c) 2019 Patrick Borger
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

public abstract class MultiWorldBootstrap : ICustomBootstrap
{
    /// <summary>
    /// List of custom worlds to create
    /// </summary>
    public List<CustomWorld> CustomWorlds { get; set; } = new List<CustomWorld>();

    private List<Type> DefaultSystems { get; }= new List<Type>();

    private static bool hasRun;

    public class CustomWorld
    {
        /// <summary>
        /// Reference to the created custom world
        /// </summary>
        public World World;

        /// <summary>
        /// Name of the custom world
        /// </summary>
        public string Name;

        /// <summary>
        /// Interfaces on systems which to add to the custom world
        /// </summary>
        public List<Type> SystemInterfaces = new List<Type>();

        /// <summary>
        /// Namespaces of systems which will be duplicated into custom World
        /// Can be used for e.g. HybridRenderer, Physics, etc.
        /// </summary>
        public List<string> SystemNamespaceToDuplicate = new List<string>();
        //public Func<List<Type>, List<Type>> DefaultSystemsToDuplicate;

        /// <summary>
        /// Self created systems which will be added to the custom world
        /// </summary>
        public List<Type> CustomSystemsToAdd { get; } = new List<Type>();

        /// <summary>
        /// All systems which will created
        /// </summary>
        public List<Type> SystemsToCreate { get; set; } = new List<Type>();
        
        /// <summary>
        /// All systems which are found in the passed namespaces
        /// </summary>
        public List<Type> DuplicatedSystems { get; } = new List<Type>();

        public CustomWorld(string name)
        {
            Name = name;
            World = new World(name);
        }
    }

    public List<Type> Initialize(List<Type> systems)
    {
        if (hasRun) return DefaultSystems;

        GetCustomWorldData(systems);

        AddAdditionalSystemsAndGroups(systems);

        BuildWorlds();

        CustomWorlds.ForEach(customWorld => customWorld.SystemsToCreate.ForEach(type => (customWorld.World.GetExistingSystem(type) as ComponentSystemGroup)?.SortSystemUpdateList()));

        (World.Active.GetOrCreateSystem(typeof(InitializationSystemGroup)) as ComponentSystemGroup)?.SortSystemUpdateList();
        (World.Active.GetOrCreateSystem(typeof(SimulationSystemGroup)) as ComponentSystemGroup)?.SortSystemUpdateList();
        (World.Active.GetOrCreateSystem(typeof(PresentationSystemGroup)) as ComponentSystemGroup)?.SortSystemUpdateList();

        DefaultSystems.AddRange(systems);

        CustomWorlds.ForEach(customWorld => customWorld.CustomSystemsToAdd.ForEach(type => DefaultSystems.Remove(type)));
        
        hasRun = true;

        return DefaultSystems;
    }

    /// <summary>
    /// Assigning the systems by their attributes and interfaces to the worlds
    /// </summary>
    private void GetCustomWorldData(List<Type> systems)
    {
        foreach (Type system in systems)
        {
            foreach (CustomAttributeData customAttribute in system.CustomAttributes)
            {
                if (customAttribute.AttributeType.Name != nameof(CreateInWorldAttribute) || customAttribute.ConstructorArguments.Count <= 0) continue;
                foreach (CustomAttributeTypedArgument worldName in customAttribute.ConstructorArguments)
                {
                    string tempWorldName = worldName.Value.ToString().Trim('"');
                    bool   worldExists   = false;
                    foreach (CustomWorld customWorld in CustomWorlds)
                    {
                        if (customWorld.Name != tempWorldName) continue;
                        customWorld.CustomSystemsToAdd.Add(system);
                        worldExists = true;
                        break;
                    }

                    if (worldExists) continue;
                    CustomWorld newCustomWorld = new CustomWorld(tempWorldName);
                    newCustomWorld.CustomSystemsToAdd.Add(system);
                    CustomWorlds.Add(newCustomWorld);
                }
            }
        }

        foreach (CustomWorld customWorld in CustomWorlds)
        {
            if (customWorld.SystemInterfaces.Count == 0) continue;
            foreach (Type system in systems)
            {
                if (system.GetInterfaces().Where(systemInterface => customWorld.SystemInterfaces.Contains(systemInterface)).TakeWhile(x => !customWorld.CustomSystemsToAdd.Contains(system)).Any())
                {
                    customWorld.CustomSystemsToAdd.Add(system);
                }
            }
        }
    }

    /// <summary>
    /// Adding update groups and the systems which should be duplicate to the creation list
    /// </summary>
    private void AddAdditionalSystemsAndGroups(List<Type> systems)
    {
        foreach (CustomWorld customWorld in CustomWorlds)
        {
            customWorld.SystemsToCreate.AddRange(customWorld.CustomSystemsToAdd);
            if (customWorld.SystemNamespaceToDuplicate.Count > 0)
            {
                foreach (string systemNamespace in customWorld.SystemNamespaceToDuplicate)
                {
                    customWorld.SystemsToCreate.AddRange(systems.FindAll(x => x.Namespace != null && x.Namespace.StartsWith(systemNamespace)));
                    customWorld.DuplicatedSystems.AddRange(systems.FindAll(x => x.Namespace != null && x.Namespace.StartsWith(systemNamespace)));
                }
                customWorld.SystemsToCreate = customWorld.SystemsToCreate.Distinct().ToList();
            }

            List<Type> recursiveGroupList = new List<Type>();

            foreach (Type systemType in customWorld.SystemsToCreate)
            {
                Type type = systemType;
                while (true)
                {
                    UpdateInGroupAttribute group = type.GetCustomAttributes(typeof(UpdateInGroupAttribute), true).Length == 0 ? null : type.GetCustomAttributes(typeof(UpdateInGroupAttribute), true)[0] as UpdateInGroupAttribute;
                    if (group == null) break;
                    if (group.GroupType == typeof(InitializationSystemGroup) || group.GroupType == typeof(SimulationSystemGroup) || group.GroupType == typeof(PresentationSystemGroup))
                    {
                    }
                    else
                    {
                        if (group.GroupType.Name != nameof(ComponentSystemGroup) && group.GroupType.GetNestedTypes().Any(x => x.Name != nameof(ComponentSystemGroup)))
                        {
                            throw new Exception($"System {group.GroupType.Name} is trying to update in a non ComponentSystemGroup class");
                        }

                        if(customWorld.CustomSystemsToAdd.Contains(systemType))
                            customWorld.CustomSystemsToAdd.Add(group.GroupType);

                        if (customWorld.SystemsToCreate.Contains(group.GroupType)) break;
                        recursiveGroupList.Add(group.GroupType);
                        type = group.GroupType;
                        continue;
                    }

                    break;
                }
            }

            customWorld.SystemsToCreate.AddRange(recursiveGroupList.Distinct().ToList());
        }
    }

    /// <summary>
    /// Creating each world with their assigned systems
    /// </summary>
    // TODO: TRY TO REWRITE IN LINQ
    public void BuildWorlds()
    {
        foreach (CustomWorld customWorld in CustomWorlds)
        {
            foreach (Type systemType in customWorld.SystemsToCreate)
            {
                UpdateInGroupAttribute updateGroup = systemType.GetCustomAttributes(typeof(UpdateInGroupAttribute), true).Length == 0 ? null : systemType.GetCustomAttributes(typeof(UpdateInGroupAttribute), true)[0] as UpdateInGroupAttribute;
                if (updateGroup != null)
                {
                    ComponentSystemGroup systemGroup = customWorld.SystemsToCreate.Contains(updateGroup.GroupType) 
                                                    || updateGroup.GroupType == typeof(InitializationSystemGroup) 
                                                    || updateGroup.GroupType == typeof(PresentationSystemGroup) 
                                                           ? customWorld.World.GetOrCreateSystem(updateGroup.GroupType) as ComponentSystemGroup 
                                                           : customWorld.World.GetExistingSystem<SimulationSystemGroup>();

                    systemGroup?.AddSystemToUpdateList(customWorld.World.GetOrCreateSystem(systemType));
                }
                else
                {
                    customWorld.World.GetOrCreateSystem<SimulationSystemGroup>().AddSystemToUpdateList(customWorld.World.GetOrCreateSystem(systemType));
                }

            }

            if (World.Active.Name == customWorld.World.Name) continue;
            World.Active.GetOrCreateSystem<InitializationSystemGroup>().AddSystemToUpdateList(customWorld.World.GetOrCreateSystem<InitializationSystemGroup>());
            World.Active.GetOrCreateSystem<SimulationSystemGroup>().AddSystemToUpdateList(customWorld.World.GetOrCreateSystem<SimulationSystemGroup>());
            World.Active.GetOrCreateSystem<PresentationSystemGroup>().AddSystemToUpdateList(customWorld.World.GetOrCreateSystem<PresentationSystemGroup>());
        }
    }
}
#endif

