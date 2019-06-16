# MultiWorldBootstrap

This is a ICustomBootstrap Extension for automatic multiple world creation. 
In its current version, it needs the `DefaultWorldInitialization` to be activated. 
*(This means you can not use `UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP`)*
```
Tested with Unity 2019.1.5f1 up to 2019.3.0a5
```

## Usage

To use MultiWorldBootstrap, the created bootstrap class has to be inherited from `MultiWorldBootstrap` instead of `ICustomBootstrap`.
```csharp
public class Bootstrap : MultiWorldBootstrap
{
}
```
---
For setting systems up to be created in self-defined worlds, there are two options.

### Attributes
```csharp
[CreateInWorld("MyCustomWorld")]
public class TestSystem : ComponentSystem
{ 
}
```
As it is possible to have a system in any amount of worlds needed, the attribute is usable multiple times:
```csharp
[CreateInWorld("MyCustomWorld1"), CreateInWorld("MyCustomWorld2")]
public class TestSystem : ComponentSystem
{ 
}
```
As the MultiWorldBootstrap will find and create the given worlds on its own, there is nothing else to do.
### Interfaces
Adding systems with interfaces needs a bit of code added to the bootstrap class.
First the system needs the interface(s) to inherit from
```csharp
public class TestSystem : ComponentSystem, ITestInterface1, ITestInterface2
{ 
}

interface ITestInterface1 { }
interface ITestInterface2 { }
```
Next the bootstrap class needs to know specifically which worlds have to add those systems.
As the wanted world and its name can not be found through the interface, it has to be manually created and given the interface types:
```csharp
public Bootstrap()
    {
        CustomWorlds.Add(new CustomWorld("TestWorld1")
                         {
                           SystemInterfaces = new List<Type>() { typeof(ITestInterface1) }
                         });
        CustomWorlds.Add(new CustomWorld("TestWorld2")
                         {
                           SystemInterfaces = new List<Type>() { typeof(ITestInterface2) }
                         });
    }
```
**Note:** *Both are usable for every system, so it is possible to use attributes for system 1 but interfaces for system 2!*

---
### Duplicating 
To use unitys default systems the custom worlds need to know in which namespace they are. 
This example adds the hybrid renderer, physics and transforms:
```csharp
public Bootstrap()
    {
        CustomWorlds.Add(new CustomWorld("TestWorld1")
                         {
                            SystemNamespaceToDuplicate = new List<string>() { "Unity.Transform", "Unity.Rendering", "Unity.Physics" }
                         });
    }
```

---
### Update order
Setting up in which order the systems will be updated hasn't changed from Unitys default behaviour.
For each system UpdateBefore, UpdateAfter and UpdateInGroup can be used.

Example:
```csharp
[UpdateInGroup(typeof(TestGroup))]
[UpdateBefore(typeof(TestSystem2))]
public class TestSystem : ComponentSystem { }

[UpdateInGroup(typeof(TestGroup))]
public class TestSystem2 : ComponentSystem { }

[UpdateInGroup(typeof(TestGroup))]
[UpdateAfter(typeof(TestSystem2))]
public class TestSystem3 : ComponentSystem { }

public class TestGroup : ComponentSystemGroup { }
```
