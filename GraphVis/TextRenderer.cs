using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

using Fusion;
using Fusion.Graphics;


namespace GraphVis
{
	public struct TransformedColoredTextured
	{
		public Vector3 Position;
		public Vector4 Color;
		public Vector2 TexCoord;

	}


	public class Quad : ICloneable
	{
		protected TransformedColoredTextured[] m_vertices;

		public Quad()
		{
		// Empty
		}


		/// <summary>Creates a new Quad</summary>
		/// <param name=”topLeft”>Top left vertex.</param>
		/// <param name=”topRight”>Top right vertex.</param>
		/// <param name=”bottomLeft”>Bottom left vertex.</param>
		/// <param name=”bottomRight”>Bottom right vertex.</param>
		public Quad( TransformedColoredTextured topLeft, TransformedColoredTextured topRight, TransformedColoredTextured bottomLeft, TransformedColoredTextured bottomRight )
		{
			m_vertices = new TransformedColoredTextured[6];
			m_vertices[0] = topLeft;
			m_vertices[1] = bottomRight;
			m_vertices[2] = bottomLeft;
			m_vertices[3] = topLeft;
			m_vertices[4] = topRight;
			m_vertices[5] = bottomRight;
		}


		/// <summary>Gets and sets the vertices.</summary>
		public TransformedColoredTextured[] Vertices
		{
			get { return m_vertices; }
			set { value.CopyTo( m_vertices, 0 ); }
		}


		/// <summary>Gets the top left vertex.</summary>
		public TransformedColoredTextured TopLeft
		{
			get { return m_vertices[0]; }
		}


		/// <summary>Gets the top right vertex.</summary>
		public TransformedColoredTextured TopRight
		{
			get { return m_vertices[4]; }
		}


		/// <summary>Gets the bottom left vertex.</summary>
		public TransformedColoredTextured BottomLeft 
		{
			get { return m_vertices[2]; }
		}


		/// <summary>Gets the bottom right vertex.</summary>
		public TransformedColoredTextured BottomRight
		{
			get { return m_vertices[5]; }
		}


/// <summary>Gets and sets the X coordinate.</summary>
		public float X
		{
			get { return m_vertices[0].Position.X; }
			set
			{
				float width = Width;
				m_vertices[0].Position.X = value;
				m_vertices[1].Position.X = value + width;
				m_vertices[2].Position.X = value;
				m_vertices[3].Position.X = value;
				m_vertices[4].Position.X = value + width;
				m_vertices[5].Position.X = value + width;
			}
		}


		/// <summary>Gets and sets the Y coordinate.</summary>
		public float Y
			{
				get { return m_vertices[0].Position.Y; }
				set
				{
					float height = Height;
					m_vertices[0].Position.Y = value;
					m_vertices[1].Position.Y = value + height;
					m_vertices[2].Position.Y = value + height;
					m_vertices[3].Position.Y = value;
					m_vertices[4].Position.Y = value;
					m_vertices[5].Position.Y = value + height;
				}
			}	


		/// <summary>Gets and sets the width.</summary>
		public float Width
		{
			get { return m_vertices[4].Position.X - m_vertices[0].Position.X; }
			set
			{
				m_vertices[1].Position.X = m_vertices[0].Position.X + value;
				m_vertices[4].Position.X = m_vertices[0].Position.X + value;
				m_vertices[5].Position.X = m_vertices[0].Position.X + value;
			}
		}


		/// <summary>Gets and sets the height.</summary>
		public float Height
		{
			get { return m_vertices[2].Position.Y - m_vertices[0].Position.Y; }
			set
			{
				m_vertices[1].Position.Y = m_vertices[0].Position.Y + value;
				m_vertices[2].Position.Y = m_vertices[0].Position.Y + value;
				m_vertices[5].Position.Y = m_vertices[0].Position.Y + value;
			}
		}


		/// <summary>Gets the X coordinate of the right.</summary>
		public float Right
		{
			get { return X + Width; }
		}


		/// <summary>Gets the Y coordinate of the bottom.</summary>
		public float Bottom
		{
			get { return Y + Height; }
		}


		/// <summary>Gets and sets the Quad’s color.</summary>
		public Vector4 Color
		{
			get { return m_vertices[0].Color; }
			set
			{
				for ( int i = 0; i < 6; i++ )
				{
					m_vertices[i].Color = value;
				}
			}
		}


		/// <summary>Writes the Quad to a string</summary>
		/// <returns>String</returns>
		public override string ToString()
		{
			string result = "X = " + X.ToString();
			result += "nY = " + Y.ToString();
			result += "nWidth = " + Width.ToString();
			result += "nHeight = " + Height.ToString();
			return result;
		}


		/// <summary>Clones the Quad.</summary>
		/// <returns>Cloned Quad</returns>
		public object Clone()
		{
			return new Quad( m_vertices[0], m_vertices[4], m_vertices[2], m_vertices[5] );
		}



		/// <summary>Represents kerning information for a character.</summary>
		public class Kerning
		{
			public int Second;
			public int Amount;
		}



		/// <summary>Represents a single bitmap character.</summary>
		public class BitmapCharacter : ICloneable
		{
			public int X;
			public int Y;
			public int Width;
			public int Height;
			public int XOffset;
			public int YOffset;
			public int XAdvance;
			public List<Kerning> KerningList = new List<Kerning>();
 
			/// <summary>Clones the BitmapCharacter</summary>
			/// <returns>Cloned BitmapCharacter</returns>
			public object Clone()
			{
				BitmapCharacter result = new BitmapCharacter();
				result.X = X;
				result.Y = Y;
				result.Width = Width;
				result.Height = Height;
				result.XOffset = XOffset;
				result.YOffset = YOffset;
				result.XAdvance = XAdvance;
				result.KerningList.AddRange( KerningList );
				return result;
			}
		}





		/// <summary>
		/// This structure contains all the info shader needs to render text. It will be passed to GPU
		/// </summary>
		[StructLayout(LayoutKind.Explicit)]
		struct BitmapCharacterSet
		{
			[FieldOffset( 0)] public int LineHeight;
			[FieldOffset( 4)] public int Base;
			[FieldOffset( 8)] public int RenderedSize;
			[FieldOffset(12)] public int Width;
			[FieldOffset(16)] public int Height;
			[FieldOffset(20)] public BitmapCharacter[] Characters;
		}
 


		/// <summary>Quad used to render bitmapped fonts</summary>
    public class FontQuad : Quad
    {
        private int m_lineNumber;
        private int m_wordNumber;
        private float m_sizeScale;
        private BitmapCharacter m_bitmapChar = null;
        private char m_character;
        private float m_wordWidth;
 
        /// <summary>Creates a new FontQuad</summary>
        /// <param name="topLeft">Top left vertex</param>
        /// <param name="topRight">Top right vertex</param>
        /// <param name="bottomLeft">Bottom left vertex</param>
        /// <param name="bottomRight">Bottom right vertex</param>
        public FontQuad( TransformedColoredTextured topLeft, TransformedColoredTextured topRight,
            TransformedColoredTextured bottomLeft, TransformedColoredTextured bottomRight )
        {
            m_vertices = new TransformedColoredTextured[6];
            m_vertices[0] = topLeft;
            m_vertices[1] = bottomRight;
            m_vertices[2] = bottomLeft;
            m_vertices[3] = topLeft;
            m_vertices[4] = topRight;
            m_vertices[5] = bottomRight;
        }
 
        /// <summary>Gets and sets the line number.</summary>
        public int LineNumber
        {
            get { return m_lineNumber; }
            set { m_lineNumber = value; }
        }
 
        /// <summary>Gets and sets the word number.</summary>
        public int WordNumber
        {
            get { return m_wordNumber; }
            set { m_wordNumber = value; }
        }
 
        /// <summary>Gets and sets the word width.</summary>
        public float WordWidth
        {
            get { return m_wordWidth; }
            set { m_wordWidth = value; }
        }
 
        /// <summary>Gets and sets the BitmapCharacter.</summary>
        public BitmapCharacter BitmapCharacter
        {
            get { return m_bitmapChar; }
            set { m_bitmapChar = (BitmapCharacter)value.Clone(); }
        }
 
        /// <summary>Gets and sets the character displayed in the quad.</summary>
        public char Character
        {
            get { return m_character; }
            set { m_character = value; }
        }
 
        /// <summary>Gets and sets the size scale.</summary>
        public float SizeScale
        {
            get { return m_sizeScale; }
            set { m_sizeScale = value; }
        }
    }


		/// <summary>Individual string to load into vertex buffer.</summary>
		struct StringBlock
		{
			public string Text;
			public RectangleF TextBox;
			public BitmapFont.Align Alignment;
			public float Size;
			public Vector4 Color;
			public bool Kerning;
 
			/// <summary>Creates a new StringBlock</summary>
			/// <param name="text">Text to render</param>
			/// <param name="textBox">Text box to constrain text</param>
			/// <param name="alignment">Font alignment</param>
			/// <param name="size">Font size</param>
			/// <param name="color">Color</param>
			/// <param name="kerning">true to use kerning, false otherwise.</param>
			public StringBlock( string text, RectangleF textBox, BitmapFont.Align alignment,
				float size, Vector4 color, bool kerning )
			{
				Text = text;
				TextBox = textBox;
				Alignment = alignment;
				Size = size;
				Color = color;
				Kerning = kerning;
			}
		}


		/// <summary>Bitmap font wrapper.</summary>
public class BitmapFont
{
    public enum Align { Left, Center, Right };
    private BitmapCharacterSet m_charSet;
    private List<FontQuad> m_quads;
    private List<StringBlock> m_strings;
    private string m_fntFile;
    private string m_textureFile;
    private Texture2D m_texture = null;
    private VertexBuffer m_vb = null;
    private const int MaxVertices = 4096;
    private int m_nextChar;
 
    /// <summary>Creates a new bitmap font.</summary>
    /// <param name="faceName">Font face name.</param>
    public BitmapFont( string fntFile, string textureFile )
    {
        m_quads = new List<FontQuad>();
        m_strings = new List<StringBlock>();
        m_fntFile = fntFile;
        m_textureFile = textureFile;
        m_charSet = new BitmapCharacterSet();
        ParseFNTFile();
    }
 
    /// <summary>Parses the FNT file.</summary>
    private void ParseFNTFile()
    {
        string fntFile = m_fntFile;
        StreamReader stream = new StreamReader( fntFile );
        string line;
        char[] separators = new char[] { ' ', '=' };
        while ( ( line = stream.ReadLine() ) != null )
        {
            string[] tokens = line.Split( separators );
            if ( tokens[0] == "info" )
            {
                // Get rendered size
                for ( int i = 1; i < tokens.Length; i++ )
                {
                    if ( tokens[i] == "size" )
                    {
                        m_charSet.RenderedSize = int.Parse( tokens[i + 1] );
                    }
                }
            }
            else if ( tokens[0] == "common" )
            {
                // Fill out BitmapCharacterSet fields
                for ( int i = 1; i < tokens.Length; i++ )
                {
                    if ( tokens[i] == "lineHeight" )
                    {
                        m_charSet.LineHeight = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "base" )
                    {
                        m_charSet.Base = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "scaleW" )
                    {
                        m_charSet.Width = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "scaleH" )
                    {
                        m_charSet.Height = int.Parse( tokens[i + 1] );
                    }
                }
            }
            else if ( tokens[0] == "char" )
            {
                // New BitmapCharacter
                int index = 0;
                for ( int i = 1; i < tokens.Length; i++ )
                {
                    if ( tokens[i] == "id" )
                    {
                        index = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "x" )
                    {
                        m_charSet.Characters[index].X = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "y" )
                    {
                        m_charSet.Characters[index].Y = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "width" )
                    {
                        m_charSet.Characters[index].Width = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "height" )
                    {
                        m_charSet.Characters[index].Height = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "xoffset" )
                    {
                        m_charSet.Characters[index].XOffset = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "yoffset" )
                    {
                        m_charSet.Characters[index].YOffset = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "xadvance" )
                    {
                        m_charSet.Characters[index].XAdvance = int.Parse( tokens[i + 1] );
                    }
                }
            }
            else if ( tokens[0] == "kerning" )
            {
                // Build kerning list
                int index = 0;
                Kerning k = new Kerning();
                for ( int i = 1; i < tokens.Length; i++ )
                {
                    if ( tokens[i] == "first" )
                    {
                        index = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "second" )
                    {
                        k.Second = int.Parse( tokens[i + 1] );
                    }
                    else if ( tokens[i] == "amount" )
                    {
                        k.Amount = int.Parse( tokens[i + 1] );
                    }
                }
                m_charSet.Characters[index].KerningList.Add( k );
            }
        }
        stream.Close();
    }
 
    /// <summary>Call when the device is destroyed.</summary>
    public void OnDestroyDevice()
    {
        if ( m_texture != null )
        {
            m_texture.Dispose();
            m_texture = null;
        }
    }
 
 
    /// <summary>Call when the device is lost.</summary>
    public void OnLostDevice()
    {
        if ( m_vb != null )
        {
            m_vb.Dispose();
            m_vb = null;
        }
    }
 
    /// <summary>Adds a new string to the list to render.</summary>
    /// <param name="text">Text to render</param>
    /// <param name="textBox">Rectangle to constrain text</param>
    /// <param name="alignment">Font alignment</param>
    /// <param name="size">Font size</param>
    /// <param name="color">Color</param>
    /// <param name="kerning">true to use kerning, false otherwise.</param>
    /// <returns>The index of the added StringBlock</returns>
    public int AddString( string text, RectangleF textBox, Align alignment, float size,
        Vector4 color, bool kerning )
    {
        StringBlock b = new StringBlock( text, textBox, alignment, size, color, kerning );
        m_strings.Add( b );
        int index = m_strings.Count - 1;
   //     m_quads.AddRange( GetProcessedQuads( index ) );
        return index;
    }
 
    /// <summary>Removes a string from the list of strings.</summary>
    /// <param name="i">Index to remove</param>
    public void ClearString( int i )
    {
        m_strings.RemoveAt( i );
    }
 
    /// <summary>Clears the list of strings</summary>
    public void ClearStrings()
    {
        m_strings.Clear();
        m_quads.Clear();
    }
 /*
    /// <summary>Gets the list of Quads from a StringBlock all ready to render.</summary>
    /// <param name="index">Index into StringBlock List</param>
    /// <returns>List of Quads</returns>
    public List<FontQuad> GetProcessedQuads( int index )
    {
        if ( index >= m_strings.Count || index < 0 )
        {
            throw new Exception( "String block index out of range." );
        }
 
        List<FontQuad> quads = new List<FontQuad>();
        StringBlock b = m_strings[index];
        string text = b.Text;
        float x = b.TextBox.X;
        float y = b.TextBox.Y;
        float maxWidth = b.TextBox.Width;
        Align alignment = b.Alignment;
        float lineWidth = 0f;
        float sizeScale = b.Size / (float)m_charSet.RenderedSize;
        char lastChar = new char();
        int lineNumber = 1;
        int wordNumber = 1;
        float wordWidth = 0f;
        bool firstCharOfLine = true;
 
        float z = 0f;
        float rhw = 1f;
 
        for ( int i = 0; i < text.Length; i++ )
        {
            BitmapCharacter c = m_charSet.Characters1];
            float xOffset = c.XOffset * sizeScale;
            float yOffset = c.YOffset * sizeScale;
            float xAdvance = c.XAdvance * sizeScale;
            float width = c.Width * sizeScale;
            float height = c.Height * sizeScale;
 
            // Check vertical bounds
            if ( y + yOffset + height > b.TextBox.Bottom )
            {
                break;
            }
 
            // Newline
            if ( text[i] == '\n' || text[i] == '\r' || ( lineWidth + xAdvance >= maxWidth ) )
            {
                if ( alignment == Align.Left )
                {
                    // Start at left
                    x = b.TextBox.X;
                }
                if ( alignment == Align.Center )
                {
                    // Start in center
                    x = b.TextBox.X + ( maxWidth / 2f );
                }
                else if ( alignment == Align.Right )
                {
                    // Start at right
                    x = b.TextBox.Right;
                }
 
                y += m_charSet.LineHeight * sizeScale;
                float offset = 0f;
 
                if ( ( lineWidth + xAdvance >= maxWidth ) && ( wordNumber != 1 ) )
                {
                    // Next character extends past text box width
                    // We have to move the last word down one line
                    char newLineLastChar = new char();
                    lineWidth = 0f;
                    for ( int j = 0; j < quads.Count; j++ )
                    {
                        if ( alignment == Align.Left )
                        {
                            // Move current word to the left side of the text box
                            if ( ( quads[j].LineNumber == lineNumber ) &&
                                ( quads[j].WordNumber == wordNumber ) )
                            {
                                quads[j].LineNumber++;
                                quads[j].WordNumber = 1;
                                quads[j].X = x + ( quads[j].BitmapCharacter.XOffset * sizeScale );
                                quads[j].Y = y + ( quads[j].BitmapCharacter.YOffset * sizeScale );
                                x += quads[j].BitmapCharacter.XAdvance * sizeScale;
                                lineWidth += quads[j].BitmapCharacter.XAdvance * sizeScale;
                                if ( b.Kerning )
                                {
                                    m_nextChar = quads[j].Character;
                                    Kerning kern = m_charSet.Characters[newLineLastChar].KerningList.Find( FindKerningNode );
                                    if ( kern != null )
                                    {
                                        x += kern.Amount * sizeScale;
                                        lineWidth += kern.Amount * sizeScale;
                                    }
                                }
                            }
                        }
                        else if ( alignment == Align.Center )
                        {
                            if ( ( quads[j].LineNumber == lineNumber ) &&
                                ( quads[j].WordNumber == wordNumber ) )
                            {
                                // First move word down to next line
                                quads[j].LineNumber++;
                                quads[j].WordNumber = 1;
                                quads[j].X = x + ( quads[j].BitmapCharacter.XOffset * sizeScale );
                                quads[j].Y = y + ( quads[j].BitmapCharacter.YOffset * sizeScale );
                                x += quads[j].BitmapCharacter.XAdvance * sizeScale;
                                lineWidth += quads[j].BitmapCharacter.XAdvance * sizeScale;
                                offset += quads[j].BitmapCharacter.XAdvance * sizeScale / 2f;
                                float kerning = 0f;
                                if ( b.Kerning )
                                {
                                    m_nextChar = quads[j].Character;
                                    Kerning kern = m_charSet.Characters[newLineLastChar].KerningList.Find( FindKerningNode );
                                    if ( kern != null )
                                    {
                                        kerning = kern.Amount * sizeScale;
                                        x += kerning;
                                        lineWidth += kerning;
                                        offset += kerning / 2f;
                                    }
                                }
                            }
                        }
                        else if ( alignment == Align.Right )
                        {
                            if ( ( quads[j].LineNumber == lineNumber ) &&
                                ( quads[j].WordNumber == wordNumber ) )
                            {
                                // Move character down to next line
                                quads[j].LineNumber++;
                                quads[j].WordNumber = 1;
                                quads[j].X = x + ( quads[j].BitmapCharacter.XOffset * sizeScale );
                                quads[j].Y = y + ( quads[j].BitmapCharacter.YOffset * sizeScale );
                                lineWidth += quads[j].BitmapCharacter.XAdvance * sizeScale;
                                x += quads[j].BitmapCharacter.XAdvance * sizeScale;
                                offset += quads[j].BitmapCharacter.XAdvance * sizeScale;
                                float kerning = 0f;
                                if ( b.Kerning )
                                {
                                    m_nextChar = quads[j].Character;
                                    Kerning kern = m_charSet.Characters[newLineLastChar].KerningList.Find( FindKerningNode );
                                    if ( kern != null )
                                    {
                                        kerning = kern.Amount * sizeScale;
                                        x += kerning;
                                        lineWidth += kerning;
                                        offset += kerning;
                                    }
                                }
                            }
                        }
                        newLineLastChar = quads[j].Character;
                    }
 
                    // Make post-newline justifications
                    if ( alignment == Align.Center || alignment == Align.Right )
                    {
                        // Justify the new line
                        for ( int k = 0; k < quads.Count; k++ )
                        {
                            if ( quads[k].LineNumber == lineNumber + 1 )
                            {
                                quads[k].X -= offset;
                            }
                        }
                        x -= offset;
 
                        // Rejustify the line it was moved from
                        for ( int k = 0; k < quads.Count; k++ )
                        {
                            if ( quads[k].LineNumber == lineNumber )
                            {
                                quads[k].X += offset;
                            }
                        }
                    }
                }
                else
                {
                    // New line without any "carry-down" word
                    firstCharOfLine = true;
                    lineWidth = 0f;
                }
 
                wordNumber = 1;
                lineNumber++;
                     
            } // End new line check
 
            // Don't print these
            if ( text[i] == '\n' || text[i] == '\r' || text[i] == '\t' )
            {
                continue;
            }
 
            // Set starting cursor for alignment
            if ( firstCharOfLine )
            {
                if ( alignment == Align.Left )
                {
                    // Start at left
                    x = b.TextBox.Left;
                }
                if ( alignment == Align.Center )
                {
                    // Start in center
                    x = b.TextBox.Left + ( maxWidth / 2f );
                }
                else if ( alignment == Align.Right )
                {
                    // Start at right
                    x = b.TextBox.Right;
                }
            }
 
            // Adjust for kerning
            float kernAmount = 0f;
            if ( b.Kerning && !firstCharOfLine )
            {
                m_nextChar = (char)text[i];
                Kerning kern = m_charSet.Characters[lastChar].KerningList.Find( FindKerningNode );
                if ( kern != null )
                {
                    kernAmount = kern.Amount * sizeScale;
                    x += kernAmount;
                    lineWidth += kernAmount;
                    wordWidth += kernAmount;
                }
            }
 
            firstCharOfLine = false;
 
            // Create the vertices
            TransformedColoredTextured topLeft = new TransformedColoredTextured(
                x + xOffset, y + yOffset, z, rhw, b.Color.ToArgb(),
                (float)c.X / (float)m_charSet.Width,
                (float)c.Y / (float)m_charSet.Height );
            TransformedColoredTextured topRight = new TransformedColoredTextured(
                topLeft.X + width, y + yOffset, z, rhw, b.Color.ToArgb(),
                (float)( c.X + c.Width ) / (float)m_charSet.Width,
                (float)c.Y / (float)m_charSet.Height );
            TransformedColoredTextured bottomRight = new TransformedColoredTextured(
                topLeft.X + width, topLeft.Y + height, z, rhw, b.Color.ToArgb(),
                (float)( c.X + c.Width ) / (float)m_charSet.Width,
                (float)( c.Y + c.Height ) / (float)m_charSet.Height );
            TransformedColoredTextured bottomLeft = new TransformedColoredTextured(
                x + xOffset, topLeft.Y + height, z, rhw, b.Color.ToArgb(),
                (float)c.X / (float)m_charSet.Width,
                (float)( c.Y + c.Height ) / (float)m_charSet.Height );
 
            // Create the quad
            FontQuad q = new FontQuad( topLeft, topRight, bottomLeft, bottomRight );
            q.LineNumber = lineNumber;
            if ( text[i] == ' ' && alignment == Align.Right )
            {
                wordNumber++;
                wordWidth = 0f;
            }
            q.WordNumber = wordNumber;
            wordWidth += xAdvance;
            q.WordWidth = wordWidth;
            q.BitmapCharacter = c;
            q.SizeScale = sizeScale;
            q.Character = text[i];
            quads.Add( q );
 
            if ( text[i] == ' ' && alignment == Align.Left )
            {
                wordNumber++;
                wordWidth = 0f;
            }
 
            x += xAdvance;
            lineWidth += xAdvance;
            lastChar = text[i];
 
            // Rejustify text
            if ( alignment == Align.Center )
            {
                // We have to recenter all Quads since we addded a
                // new character
                float offset = xAdvance / 2f;
                if ( b.Kerning )
                {
                    offset += kernAmount / 2f;
                }
                for ( int j = 0; j < quads.Count; j++ )
                {
                    if ( quads[j].LineNumber == lineNumber )
                    {
                        quads[j].X -= offset;
                    }
                }
                x -= offset;
            }
            else if ( alignment == Align.Right )
            {
                // We have to rejustify all Quads since we addded a
                // new character
                float offset = 0f;
                if ( b.Kerning )
                {
                    offset += kernAmount;
                }
                for ( int j = 0; j < quads.Count; j++ )
                {
                    if ( quads[j].LineNumber == lineNumber )
                    {
                        offset = xAdvance;
                        quads[j].X -= xAdvance;
                    }
                }
                x -= offset;
            }
        }
        return quads;
    }
 
    /// <summary>Gets the line height of a StringBlock.</summary>
    public float GetLineHeight( int index )
    {
        if ( index < 0 || index > m_strings.Count )
        {
            throw new Exception( "StringBlock index out of range." );
        }
        return m_charSet.LineHeight * ( m_strings[index].Size / m_charSet.RenderedSize );
    }
 
    /// <summary>Search predicate used to find nodes in m_kerningList</summary>
    /// <param name="node">Current node.</param>
    /// <returns>true if the node's name matches the desired node name, false otherwise.</returns>
    private bool FindKerningNode( Kerning node )
    {
        return ( node.Second == m_nextChar );
    }
 
    /// <summary>Gets the font texture.</summary>
    public Texture Texture
    {
        get { return m_texture; }
    }
  * */
}


		class TextRenderer
		{
		}
	}
}
