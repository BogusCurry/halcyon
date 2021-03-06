/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Runtime.Serialization;
using System.Security.Permissions;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate bool BoolDelegate();

    public abstract class EntityBase : ISceneEntity
    {
        /// <summary>
        /// The scene to which this entity belongs
        /// </summary>
        public Scene Scene
        {
            get { return m_scene; }
        }
        protected Scene m_scene;

        protected UUID m_uuid;

        public virtual UUID UUID
        {
            get { return m_uuid; }
            set { m_uuid = value; }
        }

        protected string m_name;

        /// <summary>
        /// The name of this entity
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        /// <summary>
        /// Signals whether this entity was in a scene but has since been removed from it.
        /// </summary>
        public bool IsDeleted
        {
            get { return m_isDeleted; }
        }
        protected bool m_isDeleted;

        public class PositionInfo
        {
            public Vector3 m_pos;
            public SceneObjectPart m_parent;
            public Vector3 m_parentPos;

            public PositionInfo()
            {
                Clear();
            }
            public PositionInfo(Vector3 pos, SceneObjectPart parent, Vector3 parentPos)
            {
                Set(pos, parent, parentPos);
            }
            public PositionInfo(PositionInfo info)
            {
                Set(info);
            }

            public void Set(PositionInfo info)
            {
                lock (this)
                {
                    m_pos = info.m_pos;
                    m_parentPos = info.m_parentPos;
                    m_parent = info.m_parent;
                }
            }
            public void Set(Vector3 pos, SceneObjectPart parent, Vector3 parentPos)
            {
                lock (this)
                {
                    m_pos = pos;
                    m_parentPos = parentPos;
                    m_parent = parent;
                }
            }
            public void SetPosition(float x, float y, float z)
            {
                lock (this)
                {
                    m_pos.X = x;
                    m_pos.Y = y;
                    m_pos.Z = z;
                }
            }
            public void Clear()
            {
                lock (this)
                {
                    m_pos = Vector3.Zero;
                    m_parentPos = Vector3.Zero;
                    m_parent = null;
                }
            }

            public Object Clone()
            {
                lock (this)
                {
                    return MemberwiseClone();
                }
            }

            public Vector3 Position
            {
                get
                {
                    lock (this)
                    {
                        return new Vector3(m_pos);
                    }
                }
                set
                {
                    lock (this)
                    {
                        m_pos = value;
                    }
                }
            }
            public SceneObjectPart Parent
            {
                get
                {
                    lock (this)
                    {
                        return m_parent;
                    }
                }
                set
                {
                    lock (this)
                    {
                        m_parent = value;
                    }
                }
            }

            internal void SetPosition(Vector3 pos)
            {
                m_pos = pos;
            }
        };

        protected PositionInfo m_posInfo;
        // Don't use getter/setter methods, make it obvious these are method function calls, not members, take a snapshot with GET, don't use .Position, etc.
        public PositionInfo GetPosInfo()
        {
            lock (m_posInfo)
            {
                return (PositionInfo)m_posInfo.Clone();
            }
        }
        public void SetPosInfo(PositionInfo info)
        {
            lock (m_posInfo)
            {
                m_posInfo.m_pos = info.m_pos;
                m_posInfo.m_parentPos = info.m_parentPos;
                m_posInfo.m_parent = info.m_parent;
            }
        }
        public bool PosInfoLockedCall(BoolDelegate func)
        {
            lock (m_posInfo)
                return func();
        }

        /// <summary>
        ///
        /// </summary>
        public virtual Vector3 AbsolutePosition
        {
            get { return m_posInfo.Position; }
            set { m_posInfo.Position = value; }
        }

        protected Vector3 m_rotationalvelocity;

        protected Quaternion m_rotation = new Quaternion(0f, 0f, 1f, 0f);

        public virtual Quaternion Rotation
        {
            get { return m_rotation; }
            set { m_rotation = value; }
        }

        protected uint m_localId;

        public virtual uint LocalId
        {
            get { return m_localId; }
            set { m_localId = value; }
        }

        /// <summary>
        /// Creates a new Entity (should not occur on it's own)
        /// </summary>
        public EntityBase()
        {
            m_uuid = UUID.Zero;

            m_posInfo = new PositionInfo();

            Rotation = Quaternion.Identity;
            m_name = "(basic entity)";
            m_rotationalvelocity = Vector3.Zero;
        }

        /// <summary>
        ///
        /// </summary>
        public abstract void UpdateMovement();

        /// <summary>
        /// Performs any updates that need to be done at each frame, as opposed to immediately.  
        /// These included scheduled updates and updates that occur due to physics processing.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Copies the entity
        /// </summary>
        /// <returns></returns>
        public virtual EntityBase Copy()
        {
            return (EntityBase) MemberwiseClone();
        }

        public abstract void SetText(string text, Vector3 color, double alpha);
    }

    //Nested Classes
    public class EntityIntersection
    {
        public Vector3 ipoint = new Vector3(0, 0, 0);
        public Vector3 normal = new Vector3(0, 0, 0);
        public Vector3 AAfaceNormal = new Vector3(0, 0, 0);
        public int face = -1;
        public bool HitTF = false;
        public SceneObjectPart obj;
        public float distance = 0;

        public EntityIntersection()
        {
        }

        public EntityIntersection(Vector3 _ipoint, Vector3 _normal, bool _HitTF)
        {
            ipoint = _ipoint;
            normal = _normal;
            HitTF = _HitTF;
        }
    }
}
