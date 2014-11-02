pooled-list
===========

PooledList

This is a Unity 4.6 / ugui compatible pooled list component. Canvases each have a limit of 65535 vertices, and instantiating hundreds of tile prefabs can cause noticeable hitches and slowdown to your game. The goal was to decrease the number of active UI objects to improve performance.

This repo includes example implementations in the form of: PooledList, PooledItem, and PooledData




Extending AbstractPoolList to create a Pooled List
--

Your first step should be creating a PooledList implementation. The PooledList example can be a good starting point.

public class MyPooledList : AbstractPooledList<MyPooledItem, MyPooledData>

What we're doing here is extending AbstractPooledList and specifying generics to tell the list what the tile prefab class and data class will be.

Implementing an IPooledItem
--

Next we need to create the MonoBehaviour you're using as a Tile. Odds are you already have this MB created, and you won't need to do much.

Your class declaration will look like this:
public class MyPooledItem : MonoBehaviour, IPooledItem<MyPooledData>

This is only slightly more complex. Once again we're using a generic to specify what our data type is. We also need to implement two methods: Key and SetData. Key is a unique identifer object we use to identify which tile we are dealing with. SetData passes in an instance of the data class you have, and sets your Key. My tiles are using database specified UUIDs, or autoincremented mysql ints/longs.

Implementing an IPooledData
--

Third, we will be adding an interface to our data class. This is a very simple data interface, and it just specifies that you need a "Key" value. The rest is up to you. The PooledItem class you just created will know what to do with this class.

public class MyPooledData : IPooledListData

Just implement the Key getter and you're set! That's literally all of the code you need to write.


On Prefab/Scene creation
--
Take a look at pooltest.unity. It should help you figure out the setup.

Now comes the Scene work. This assumes you already have a Grid, ScrollRect, scrollbars, etc. Set up in your scene or prefab. If you need help understanding how to use these components, I'm happy to help, but I'd head to the Unity UI forum instead!

If you're using this component, you *probably* want a Canvas alongside it. A grid can create a good number of vertices, and even the best object pool is limited by how complex your tiles are. It will separate the draw calls of this grid in to a separate canvas, which could even help your performance if you aren't doing anything else at the time (by localizing the draws and not redrawing other unused portions of the UI!)

Setting up your component
--
Go ahead and Add Component on the parent game object you want to use to specify the list. Odds are you have set up a grid area with scrollbars and a parent recttransform already, so just claim that gameobject and tack it on with the aforementioned canvas, if you haven't done that already.

It is recommended your layout looks simialr to this:

PooledList/Canvas
-ScrollRect/Image/Mask
--Grid
-HorizontalScrollbar
-VerticalScrollbar

Your PooledList will need references to the ScrollRect, Grid and a Prefab using your MyPooledItem component.
Next, specify a PoolSize and a Buffer. PoolSize should be equal to the number of tiles shown on screen total, plus buffer area. The Buffer area is in both directions, so if you are using a vertical grid it will buffer that many rows above and below the current viewable area.

Make certain you have your Scrollbar set in the ScrollRect. 

List Direction
--
List direction is dictated by Grid constraint. PooledList does **not** support Flexible grid constraints. It must be fixed. Keep in mind that you will want a Horizontal StartAxis for a vertical list, and a Vertical StartAxis for a horizontal list. That's just specifying how to add tiles to the grid, and in which direction. You want to fill horizontally, and scroll up<>down, or fill vertically, and scroll left<>right.

Thank you
--
As I've just updated this, it probably has bugs! I've tested this in my own game and the test scene has seen a considerable amount of work but I've only given this about 20 hours of work. I'm hoping people like yourself can contribute and report bugs so I can get this fixed up!
