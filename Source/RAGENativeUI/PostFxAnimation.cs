namespace RAGENativeUI
{
#if RPH1
    extern alias rph1;
    using IAddressable = rph1::Rage.IAddressable;
#else
    /** REDACTED **/
#endif

    using System;
    using System.Linq;
    using System.Diagnostics;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    using Rage;
    using Rage.Native;

    using RAGENativeUI.Memory;

    // defined in animpostfx.ymt
    public unsafe sealed class PostFxAnimation : IAddressable
#if !RPH1
        /** REDACTED **/
#endif
    {
        internal readonly CAnimPostFX* Native;
        private readonly int index = -1;

        public uint Hash { get { return Native->Name; } }
        public string Name
        {
            get
            {
                if (KnownNames.PostFxAnimations.Dictionary.TryGetValue(Native->Name, out string n))
                {
                    return n;
                }

                return $"0x{Native->Name:X8}";
            }
        }

        public bool IsActive
        {
            get
            {
                return IsValid && (GameFunctions.IsAnimPostFXActive(*GameMemory.AnimPostFXManager, &Native->Name) & 1) != 0;
            }
        }

        public IntPtr MemoryAddress { get { return (IntPtr)Native; } }
        public int Index { get { return index; } }
        public PostFxAnimationLayerBlend LayerBlend { get; }
        public PostFxAnimationLayersCollection Layers { get; }

        public bool IsValid => Native != null;

        private PostFxAnimation(CAnimPostFX* native)
        {
            Native = native;
            if (IsValid)
            {
                index = unchecked((int)((long)Native - (long)(*GameMemory.AnimPostFXManager)->Effects.Items) / sizeof(CAnimPostFX));
            }
            LayerBlend = new PostFxAnimationLayerBlend(this);
            Layers = new PostFxAnimationLayersCollection(this);

            Cache.Add(this);
        }

        public void Start(int duration, bool looped)
        {
            if (!IsValid)
                return;

            GameFunctions.StartAnimPostFX(*GameMemory.AnimPostFXManager, &Native->Name, duration, looped, 0, 0, 0);
        }

        public void Stop()
        {
            if (!IsValid)
                return;

            GameFunctions.StopAnimPostFX(*GameMemory.AnimPostFXManager, &Native->Name);
        }


        public static void StopAll()
        {
            N.StopAllScreenEffects();
        }

        public static PostFxAnimation GetByName(string name)
        {
            Throw.IfNull(name, nameof(name));

            uint hash = RPH.Game.GetHashKey(name);
            KnownNames.PostFxAnimations.Dictionary[hash] = name;
            return GetByHash(hash);
        }

        public static PostFxAnimation GetByHash(uint hash)
        {
            if (Cache.Get(hash, out PostFxAnimation p))
            {
                return p;
            }
            else
            {
                CAnimPostFX* native = (CAnimPostFX*)GameFunctions.GetAnimPostFXByHash(*GameMemory.AnimPostFXManager, &hash);

                if (native != null)
                {
                    return new PostFxAnimation(native);
                }
            }

            return null;
        }

        public static PostFxAnimation GetByIndex(int index)
        {
            Throw.IfOutOfRange(index, 0, NumberOfPostFxAnimations - 1, nameof(index));
            
            CAnimPostFX* native = &(*GameMemory.AnimPostFXManager)->Effects.Items[index];

            if (Cache.Get(native->Name, out PostFxAnimation p))
            {
                return p;
            }
            else
            {
                return new PostFxAnimation(native);
            }
        }

        public static PostFxAnimation[] GetAll()
        {
            CAnimPostFXManager* mgr = *GameMemory.AnimPostFXManager;
            PostFxAnimation[] effects = new PostFxAnimation[mgr->Effects.Count];
            for (ushort i = 0; i < mgr->Effects.Count; i++)
            {
                effects[i] = GetByIndex(i);
            }

            return effects;
        }
        
        public static PostFxAnimation LastAnimation
        {
            get
            {
                CAnimPostFX* native = (*GameMemory.AnimPostFXManager)->GetLastActiveEffect();
                if (native != null)
                {
                    return GetByHash(native->Name);
                }

                return null;
            }
        }

        public static PostFxAnimation CurrentAnimation
        {
            get
            {
                CAnimPostFX* native = (*GameMemory.AnimPostFXManager)->GetCurrentActiveEffect();
                if (native != null)
                {
                    return GetByHash(native->Name);
                }

                return null;
            }
        }

        public static int NumberOfPostFxAnimations
        {
            get
            {
                return (*GameMemory.AnimPostFXManager)->Effects.Count;
            }
        }
    }

    public enum PostFxAnimationLayerAnimationMode : uint
    {
        InHoldOut = 0, // POSTFX_IN_HOLD_OUT
        EaseInHoldEaseOut = 1, // POSTFX_EASE_IN_HOLD_EASE_OUT
        EaseIn = 2, // POSTFX_EASE_IN
    }

    public enum PostFxAnimationLayerLoopMode : uint
    {
        HoldOnly = 1, // POSTFX_LOOP_HOLD_ONLY
        None = 2, // POSTFX_LOOP_NONE
    }

    public unsafe sealed class PostFxAnimationLayer : IAddressable
    {
        internal readonly CAnimPostFX.Layer* Native;

        public IntPtr MemoryAddress { get { return (IntPtr)Native; } }

        public TimeCycleModifier Modifier
        {
            get
            {
                return Native->ModifierName == 0 ? null : TimeCycleModifier.GetByHash(Native->ModifierName);
            }
        }

        public uint StartDelayDuration { get { return Native->StartDelayDuration; } }
        public uint InDuration { get { return Native->InDuration; } }
        public uint HoldDuration { get { return Native->HoldDuration; } }
        public uint OutDuration { get { return Native->OutDuration; } }
        public PostFxAnimationLayerAnimationMode AnimationMode { get { return (PostFxAnimationLayerAnimationMode)Native->AnimMode; } }
        public PostFxAnimationLayerLoopMode LoopMode { get { return (PostFxAnimationLayerLoopMode)Native->LoopMode; } }

        internal PostFxAnimationLayer(CAnimPostFX.Layer* native)
        {
            Native = native;
        }
        
        public bool IsValid()
        {
            return MemoryAddress != IntPtr.Zero;
        }
    }

    public unsafe sealed class PostFxAnimationLayersCollection : IReadOnlyCollection<PostFxAnimationLayer>, IEnumerable<PostFxAnimationLayer>
    {
        private readonly PostFxAnimation animation;
        private readonly PostFxAnimationLayer[] layers;

        public int Count { get { return animation.Native->FXStack.LayersCount; } }
        public PostFxAnimationLayer this[int index]
        {
            get
            {
                Throw.IfOutOfRange(index, 0, Count - 1, nameof(index));

                if (layers[index] == null)
                {
                    PostFxAnimationLayer l = new PostFxAnimationLayer(animation.Native->FXStack.Layers[index]);
                    layers[index] = l;
                    return l;
                }
                else
                {
                    return layers[index];
                }
            }
        }

        internal PostFxAnimationLayersCollection(PostFxAnimation anim)
        {
            animation = anim;
            layers = new PostFxAnimationLayer[Count];
        }

        public IEnumerator<PostFxAnimationLayer> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public unsafe sealed class PostFxAnimationLayerBlend : IAddressable
    {
        private readonly PostFxAnimation animation;
        internal readonly CAnimPostFX.LayerBlend* Native;

        public IntPtr MemoryAddress { get { return (IntPtr)Native; } }

        private PostFxAnimationLayer layerA;
        public PostFxAnimationLayer LayerA
        {
            get
            {
                if (Native->LayerA == null)
                {
                    return null;
                }
                else
                {
                    if (layerA != null)
                    {
                        return layerA;
                    }
                    else
                    {
                        for (int i = 0; i < animation.Layers.Count; i++)
                        {
                            if (animation.Layers[i].MemoryAddress == (IntPtr)Native->LayerA)
                            {
                                layerA = animation.Layers[i];
                                break;
                            }
                        }

                        return layerA;
                    }
                }
            }
        }

        private PostFxAnimationLayer layerB;
        public PostFxAnimationLayer LayerB
        {
            get
            {
                if (Native->LayerB == null)
                {
                    return null;
                }
                else
                {
                    if (layerB != null)
                    {
                        return layerB;
                    }
                    else
                    {
                        for (int i = 0; i < animation.Layers.Count; i++)
                        {
                            if (animation.Layers[i].MemoryAddress == (IntPtr)Native->LayerB)
                            {
                                layerB = animation.Layers[i];
                                break;
                            }
                        }

                        return layerB;
                    }
                }
            }
        }

        public float FrequencyNoise { get { return Native->FrequencyNoise; } }
        public float AmplitudeNoise { get { return Native->AmplitudeNoise; } }
        public float Frequency { get { return Native->Frequency; } }
        public float Bias { get { return Native->Bias; } }
        public bool Disabled { get { return Native->Disabled; } }

        internal PostFxAnimationLayerBlend(PostFxAnimation anim)
        {
            animation = anim;
            Native = &animation.Native->FXStack.LayerBlend;
        }
        
        public bool IsValid()
        {
            return MemoryAddress != IntPtr.Zero;
        }
    }
}

