--! \file tx_buffer.vhd
--! \brief Einfaches 3 Byte Register, welches als Buffer f�r das Bootloader Response Paket dient. 
--! 
--! H�lt den letzten Status und sorgt daf�r, dass die UART TX Komponente das gesamte zu sendende Bootloader Response Paket versendet. 
--! Grund f�r die Existenz des Buffers ist, damit eine �nderung des Status w�hrend des �bertragens keine Fehl�bertraguns zufolge hat.   

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

--! \brief Einfaches 3 Byte Register, welches als Buffer f�r das Bootloader Response Paket dient. 
--! 
--! H�lt den letzten Status und sorgt daf�r, dass die UART TX Komponente das gesamte zu sendende Bootloader Response Paket versendet. 
--! Grund f�r die Existenz des Buffers ist, damit eine �nderung des Status w�hrend des �bertragens keine Fehl�bertraguns zufolge hat.  
entity tx_buffer is
    Port ( clk              : in  STD_LOGIC;      -- Clock input
           data_in          : in  STD_LOGIC_VECTOR (23 downto 0); -- Input data
           data_out         : out STD_LOGIC_VECTOR (23 downto 0) := X"eeeeee";  -- Output data
           o_rdy_to_fetch   : out std_logic;
           i_flush          : in std_logic;
           i_tx_trig        : in std_logic
        );
end tx_buffer;

architecture Behavioral of tx_buffer is
    signal tx_buffer_reg : STD_LOGIC_VECTOR (23 downto 0) := (others => '0');  -- Internal tx_buffer register
	signal w_rdy_to_fetch 	: std_logic := '0';
   signal wen : STD_LOGIC := '0'; 
begin

    
    process(clk)
    begin

        -- wen <= i_flush or i_tx_trig;
        wen <= i_tx_trig;
        o_rdy_to_fetch <=  w_rdy_to_fetch;

        if rising_edge(clk) then
            if wen = '1' then
                tx_buffer_reg <= data_in;  -- Update tx_buffer only when write enable is active
                w_rdy_to_fetch <= '1';
            else
                w_rdy_to_fetch <= '0';
            end if;
        end if;
    end process;
    data_out <= tx_buffer_reg;
      -- Output the tx_buffered data

end Behavioral;
