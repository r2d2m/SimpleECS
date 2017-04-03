﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SimpleECS.Internal	// putting it in this name space to clean up Intellisense and hide unnecessary classes
{
	public delegate void EntityEvent<E>(Entity sender, Entity reciever, E args);

	[DisallowMultipleComponent]
	public sealed class EntityManager : MonoBehaviour
	{
		public static bool loaded; // static so I can query this value without the singleton being loaded

		public static EntityManager instance; 	// yes it's singleton pattern

		[HideInInspector]
		public int totalEntityCount; 	// this and the systems list below are only used for the custom inspector nothing else
		[HideInInspector]				// entity count is updated by the entity on awake
		public List<BaseEntitySystem> Systems = new List<BaseEntitySystem>();	// systems is added to by the system on awake

		int ComponentCount = -1; 	// Cache of how many components are in the Assembly
		public Dictionary<System.Type, int> componentIDLookup = new Dictionary<System.Type, int>();	// Lookup table for component ID's
		public Dictionary<int , object> entityEventLookup = new Dictionary<int, object>();				// Lookup table for Events, same reason as above
		public Stack<Group> groups = new Stack<Group>(); 		// Keeps track of all groups so I can clear them out when not needed

		public Action UpdateCallback = delegate {};			// this is the actual update callback, Systems just register thier Update calls to this
		public Action FixedUpdateCallback = delegate {};	// same as above except with fixed update

		void Awake()
		{
			if (loaded) // loaded means another instance exists so destroy this instance
			{
				Destroy(this);
				return;
			}
			instance = this;
			loaded = true;
			DontDestroyOnLoad(gameObject);

			var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();	// populates component IDs
			foreach (var assembly in assemblies)
			{
				foreach(var type in assembly.GetTypes())
				{
					if (type.IsSubclassOf(typeof(EntityComponent)) && !type.IsAbstract)
					{
						componentIDLookup.Add(type, componentIDLookup.Count);
					}
				}
			}
			ComponentCount = componentIDLookup.Count;
		}


		void OnDestroy()
		{
			if (instance != this) return;
			UpdateCallback = null;			// clear out delegates to avoid possible weak references
			FixedUpdateCallback = null;
			loaded = false;
		}

		void Update()
		{
			UpdateCallback();	
		}

		void FixedUpdate()
		{
			FixedUpdateCallback();
		}

		int newEntityId;				
		public int GetEntityID	// small simple function to doll out unique Entity ID's
		{
			get {return ++newEntityId;}
		}

		public int GetComponentCount()	// Returns Total amount of Entity Components
		{
			return ComponentCount;
		}

		int GetEntityComponentID<C>() // Retrieve Component ID's, the ID is the array position of the component inside the entity's component array
		{
			int id;
			if (componentIDLookup.TryGetValue(typeof(C), out id))
				return id;
			return -1;
		}

		#region GetGroups

		public Group<C> GetGroup<C>() where C : EntityComponent<C>	// Returns an instance of a group, gets called during Group Instantiation
		{
			Group<C> newGroup = new Group<C>(GetEntityComponentID<C>());
			groups.Push(newGroup);
			return newGroup;
		}

		public Group<C1, C2> GetGroup<C1, C2>() 
			where C1 : EntityComponent<C1>
			where C2 : EntityComponent<C2>
		{
			Group<C1, C2> newGroup = new Group<C1,C2>();
			groups.Push(newGroup);
			return newGroup;
		}

		public Group<C1, C2, C3> GetGroup<C1, C2, C3>() 
			where C1 : EntityComponent<C1>
			where C2 : EntityComponent<C2>
			where C3 : EntityComponent<C3>
		{
			Group<C1, C2, C3> newGroup = new Group<C1,C2,C3>();
			groups.Push(newGroup);
			return newGroup;
		}

		public Group<C1, C2, C3, C4> GetGroup<C1, C2, C3, C4>() 
			where C1 : EntityComponent<C1>
			where C2 : EntityComponent<C2>
			where C3 : EntityComponent<C3>
			where C4 : EntityComponent<C4>
		{
			Group<C1, C2, C3, C4> newGroup = new Group<C1,C2,C3,C4>();
			groups.Push(newGroup);
			return newGroup;
		}

		public Group<C1, C2, C3, C4, C5> GetGroup<C1, C2, C3, C4, C5>() 
			where C1 : EntityComponent<C1>
			where C2 : EntityComponent<C2>
			where C3 : EntityComponent<C3>
			where C4 : EntityComponent<C4>
			where C5 : EntityComponent<C5>
		{
			Group<C1, C2, C3, C4, C5> newGroup = new Group<C1,C2,C3,C4,C5>();
			groups.Push(newGroup);
			return newGroup;
		}
		#endregion

		/// <summary>
		/// Entity Events
		/// </summary>

		public class EventHolder<E>	// helper class to store Event delegate
		{
			public EntityEvent<E> entityEvent = delegate{};
		}

		public class EventID
		{
			protected static int id;
		}

		public class EventID<C>:EventID	// cheap trick to lookup Events using Int instead of Type
		{
			static bool hasID;
			static int _id;
			public static int ID
			{
				get
				{
					if (hasID) return _id;
					hasID = true;
					_id = id++;
					return _id;
				}
			}
		}

		public void AddEvent<E>(EntityEvent<E> callback) // Adds an Event Listener
		{
			object e;
			if (entityEventLookup.TryGetValue(EventID<E>.ID, out e))
			{
				((EventHolder<E>)e).entityEvent += callback;
				return;
			}

			EventHolder<E> newEvent = new EventHolder<E>();
			newEvent.entityEvent += callback;

			entityEventLookup.Add(EventID<E>.ID, newEvent);
		}

		public void RemoveEvent<E>(EntityEvent<E> callback)	// Removes Event Listener
		{
			object e;
			if (entityEventLookup.TryGetValue(EventID<E>.ID, out e))
			{	
				((EventHolder<E>)e).entityEvent -= callback;
			}
		}

		public void InvokeEvent<E>(Entity sender, Entity reciever, E args)	// Calls Event with Arguments
		{
			object e;
			if (entityEventLookup.TryGetValue(EventID<E>.ID, out e))
				((EventHolder<E>)e).entityEvent(sender, reciever, args);
		}
	}
		 
	/// 
	/// 
	///		ENTITY MANAGER INSPECTOR
	///
	///

	#if UNITY_EDITOR
	[CustomEditor(typeof(EntityManager))]
	public class EntityManagerInspector : Editor
	{
		EntityManager manager;

		void OnEnable()
		{
			manager = (EntityManager)target;
		}

		public override void OnInspectorGUI ()
		{
			if (Application.isPlaying)
			{
				EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

				EditorGUILayout.LabelField(string.Format("Total Entities : {0}", manager.totalEntityCount));
				EditorGUILayout.LabelField(string.Format("Total Systems : {0}", manager.Systems.Count));
				EditorGUILayout.LabelField(string.Format("Groups : {0}", manager.groups.Count));
				EditorGUILayout.LabelField(string.Format("Component Types : {0}", manager.componentIDLookup.Count));

				EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);
				int count = 1;
				foreach(var system in manager.Systems)
				{
					if (system is IEntityCount)
					{
						if (system.enabled && system.gameObject.activeInHierarchy)
						{
							EditorGUILayout.BeginHorizontal();
								int eCount = (system as IEntityCount).GetEntityCount();
								EditorGUILayout.LabelField(string.Format("{0} : {1}", count, system.GetType()));
								EditorGUILayout.LabelField(string.Format("| {0} Entities", eCount), GUILayout.MaxWidth(128f));
							EditorGUILayout.EndHorizontal();
						}
						else
						{
							EditorGUILayout.BeginHorizontal();
								EditorGUILayout.LabelField(string.Format("{0} : {1}", count, system.GetType()));
								EditorGUILayout.LabelField("| Disabled" , GUILayout.MaxWidth(128f));
							EditorGUILayout.EndHorizontal();
						}
					}
					else
					{
						if (system.enabled && system.gameObject.activeInHierarchy)
						{
							EditorGUILayout.LabelField(string.Format("{0} : {1}", count, system.GetType()));	
						}
						else
						{
							EditorGUILayout.BeginHorizontal();
							EditorGUILayout.LabelField(string.Format("{0} : {1}", count, system.GetType()));
							EditorGUILayout.LabelField("| Disabled" , GUILayout.MaxWidth(128f));
							EditorGUILayout.EndHorizontal();	
						}
					}
					count ++;
				}
				EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);
				EditorUtility.SetDirty(target);
			}
			else
			{
				EditorGUILayout.HelpBox("SimpleECS Manager class, needs to be in scene for SimpleECS to work. Automatically sets itself to 'Don't Destroy on Load'. When transitioning to a new scene will automatically destroy any duplicate Entity Managers.", MessageType.Info); 
			}
		}

		string GetEnabled(BaseEntitySystem system)
		{
			if (system.enabled && system.gameObject.activeInHierarchy)
			{
				return "Enabled";	
			}
			return "Disabled";
		}

}
#endif

}