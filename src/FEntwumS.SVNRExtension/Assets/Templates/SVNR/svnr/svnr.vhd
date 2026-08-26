-------------------------------------------------------------------------------
-- SVNR auf iceduino
-- Hardware Adressen:
--
-- 0004: Input von SW1 bis SW7:
--       Bit  15  14  13  12  11  10  09  08  07  06  05  04  03  02  01  00
--            --  --  --  --  --  --  --  -- SW7 SW6 --  SW5 SW4 SW3 SW2 SW1
-- 0005: Output an 2 Hexdigit Display an PMOD1
-- 0006: Output an LEDS D1...D8
-- 0007: Output PMOD3.1 Pin 9 (P2_3) für Beep
--
-- 03C0 .. 03FF WS2812 8x8 RGB Matrix Ausgang PMOD3.1 Pin 11 (P1_3) f. Dout
--       eine 16 BIt Speicherstelle pro Pixel:
--       Bit 15..12: reserviert
--       Bit 11..8: Rot  Wert (0000 => aus, 1111 = max. Helligkeit)
--       Bit  7..4: Grün Wert (0000 => aus, 1111 = max. Helligkeit)
--       Bit  3..0: Blau Wert (0000 => aus, 1111 = max. Helligkeit)
-------------------------------------------------------------------------------
library IEEE;
use IEEE.STD_LOGIC_1164.all;

entity svnr is
  port (
    clk         : in  std_logic;
    cpu_en      : in std_logic;
    ram_address_ext : in std_logic_vector(15 downto 0);
    ram_data_in_ext : in std_logic_vector(15 downto 0);
    wnr_ext         : in std_logic;
    addrstrb_ext    : in std_logic;
    reset_ext       : in std_logic; -- TODO: make external reset work with svnr

    register_data_in_ext   : in std_logic_vector(15 downto 0);
    register_load_in_ext   : in std_logic;
    register_sel_in_ext   : in std_logic_vector(2 downto 0);
    register_data_out_ext   : out std_logic_vector(15 downto 0);

    program_counter : out std_logic_vector(15 downto 0);
    ram_data_out_ext : out std_logic_vector(15 downto 0);
    cpu_step_fin    : out std_logic;
    -- PMOD 3 
    beep        : out std_logic;         -- # PMOD 3.1 P2_3 Pin 9
    ws2812_out  : out std_logic;         -- # PMOD3.0 P1_3  Pin 11
    -- Simple I/Os --
    led         : out std_logic_vector(7 downto 0);
    btn         : in  std_logic_vector(4 downto 0);
    sw          : in  std_logic_vector(1 downto 0);
    -- 2 Seg Di splay an PMOD 1
    zehner      : out std_logic_vector(3 downto 0);
    einer       : out std_logic_vector(3 downto 0)
    );  

end svnr;

architecture STRUCTURE of svnr is

  signal ALU_greater_zero                          : std_logic;
  signal ALU_operand_c_out                         : std_logic_vector (15 downto 0);
  signal Akku_q                                    : std_logic_vector (15 downto 0);
  signal BefehlsregUpperByte_Zero_dout             : std_logic_vector (15 downto 0);
  signal Befehlsregister_OPCode_q                  : std_logic_vector (7 downto 0);
  signal Befehlsregister_Operand_q                 : std_logic_vector (7 downto 0);
  signal Befehlsregister_Operand_q_16              : std_logic_vector (15 downto 0);
  signal Hilfsregister_q                           : std_logic_vector (15 downto 0);
  signal Programmzaehler_q                         : std_logic_vector (15 downto 0);
  signal Speicher_LowerByte_Dout                   : std_logic_vector (7 downto 0);
  signal Speicher_LowerByte_Dout_16                : std_logic_vector (15 downto 0);
  signal Speicher_UpperByte_n_Dout                 : std_logic_vector (7 downto 0);
  signal Steuerwerk_address_select                 : std_logic_vector (1 downto 0);
  signal Steuerwerk_akku_data_in_select            : std_logic_vector (1 downto 0);
  signal Steuerwerk_alu_operand_b_select           : std_logic;
  signal Steuerwerk_load_akku                      : std_logic;
  signal Steuerwerk_load_hilfsregister             : std_logic;
  signal Steuerwerk_load_programmzaehler           : std_logic;
  signal Steuerwerk_programmzaehler_data_in_select : std_logic_vector (1 downto 0);
  signal Steuerwerk_speicher_data_in_select        : std_logic_vector (1 downto 0);
  signal Steuerwerk_strobe                         : std_logic;
  signal Steuerwerk_wr                             : std_logic;
  signal alu_n_0_equal_zero                        : std_logic;
  signal alu_n_0_less_zero                         : std_logic;
  signal bus_tri_0_output                          : std_logic_vector (15 downto 0);
  signal Programmzaehler_inc                       : std_logic_vector (15 downto 0);
  signal mux_2_n_0_result                          : std_logic_vector (15 downto 0);
  signal mux_4_n_0_result                          : std_logic_vector (15 downto 0);
  signal mux_4_n_1_result                          : std_logic_vector (15 downto 0);
  signal address_multiplexer_out                   : std_logic_vector (15 downto 0);
  signal programmzaehler_mux_out                   : std_logic_vector (15 downto 0);
  signal steuerautomat_0_calculate                 : std_logic;
  signal steuerautomat_0_clear_register            : std_logic;
  signal steuerautomat_0_load_befehlsregister      : std_logic;
  signal stim_0_reset                              : std_logic;
  signal stim_n_0_clk_t                            : std_logic;
  signal mem_init_running                          : std_logic;  -- Vom speicher
  signal anzeige_2_hexdigits                       : std_logic_vector(7 downto 0);  -- Vom speicher
  signal clk_to_mem                                : std_logic;
  signal r_clk_out                                 : std_logic;
  signal r_ext_ram_mux_out                         : std_logic_vector(33 downto 0) := "0000000000000000000000000000000000";
                                        -- zum Reset
  signal reset_mem                                 : std_logic;  -- extra reset
  signal steuerautomat_reset_int                   : std_logic;
  -- für speicher

  signal data0_ext_ram_ctrl_mux					   : std_logic_vector (1 downto 0);
  signal data1_ext_ram_ctrl_mux					   : std_logic_vector (1 downto 0);

  -- register
  signal r_ext_akku_data_mux_out    : std_logic_vector (15 downto 0);
  signal r_ext_befehlsreg_data_mux_out  : std_logic_vector (15 downto 0);
  signal r_ext_befehlsreg_data_mux_in   : std_logic_vector (15 downto 0);
  signal r_ext_pc_data_mux_out    : std_logic_vector (15 downto 0);
  signal r_ext_hilfsreg_data_mux_out    : std_logic_vector (15 downto 0);

  signal ext_reg_load_demux_out : std_logic_vector (3 downto 0);
  
  -- signal r_ext_akku_load_mux_out    : std_logic;
  -- signal r_ext_befehlsreg_load_mux_out  : std_logic;
  -- signal r_ext_hilfsreg_load_mux_out    : std_logic;
  -- signal r_ext_pc_load_mux_out    : std_logic;

  signal load_register   : std_logic_vector (3 downto 0);
  signal Steuerwerk_load_register   : std_logic_vector (3 downto 0);
  signal ext_load_register   : std_logic_vector (0 downto 0);

  signal r_befehlsregister_q  : std_logic_vector (15 downto 0);
  signal r_alu_status  : std_logic_vector (15 downto 0);
  signal reg_data_mux_others    : std_logic_vector (15 downto 0);

begin
  -- route debugging signals to output

  program_counter <= Programmzaehler_q;
  ram_data_out_ext <= bus_tri_0_output;

  ext_load_register(0) <= register_load_in_ext;
  Steuerwerk_load_register(0) <= Steuerwerk_load_akku;
  Steuerwerk_load_register(1) <= steuerautomat_0_load_befehlsregister;
  Steuerwerk_load_register(2) <= Steuerwerk_load_hilfsregister;
  Steuerwerk_load_register(3) <= Steuerwerk_load_programmzaehler;

  r_alu_status(15 downto 3) <= "0000000000000";
  r_alu_status(2) <= alu_n_0_equal_zero;
  r_alu_status(1) <= ALU_greater_zero;
  r_alu_status(0) <= alu_n_0_less_zero;
  
  ext_reg_data_mux : entity work.mux_8_n
    generic map(
      size => 16
    )
    port map(
      data0 => Akku_q,
      data1 => r_befehlsregister_q,
      data2 => Hilfsregister_q(15 downto 0),
      data3 => Programmzaehler_q(15 downto 0),
      data4 => r_alu_status,      
      data5 => reg_data_mux_others,  
      data6 => reg_data_mux_others,  
      data7 => reg_data_mux_others,       
      result => register_data_out_ext,
      sel => register_sel_in_ext
    );

  ext_reg_load_demux : entity work.demux_4_n
    generic map(
      size => 1
    )
    port map(
      data0 => ext_reg_load_demux_out(0 downto 0),
      data1 => ext_reg_load_demux_out(1 downto 1),
      data2 => ext_reg_load_demux_out(2 downto 2),
      data3 => ext_reg_load_demux_out(3 downto 3),
      input_data => ext_load_register(0 downto 0),
      sel => register_sel_in_ext(1 downto 0)
    );

  -- --------------- Akku ---------------------------------------------
  ext_akku_data_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => register_data_in_ext,
      data1 => mux_4_n_0_result(15 downto 0),
      result => r_ext_akku_data_mux_out,
      sel => cpu_en
    );
  ext_akku_load_mux : entity work.mux_2_n
    generic map(
      size => 1
    )
    port map(
      data0 => ext_reg_load_demux_out(0 downto 0),
      data1 => Steuerwerk_load_register(0 downto 0),
      result => load_register(0 downto 0),
      sel => cpu_en
    );

  Akku : entity work.register_en
    generic map (
      size        => 16,
      clear_value => 0)
    port map (
      clock_t => stim_n_0_clk_t,
      clr     => steuerautomat_0_clear_register,
      -- load    => Steuerwerk_load_akku,
      load    => load_register(0),
      -- data    => mux_4_n_0_result(15 downto 0),
      data    => r_ext_akku_data_mux_out,
      q       => Akku_q);

  -- --------------- Befehlsregister ---------------------------------------------
  
  r_ext_befehlsreg_data_mux_in(15 downto 8) <= Speicher_UpperByte_n_Dout(7 downto 0);
  r_ext_befehlsreg_data_mux_in(7 downto 0) <= Speicher_LowerByte_Dout(7 downto 0);

  r_befehlsregister_q(15 downto 8) <= Befehlsregister_OPCode_q(7 downto 0);
  r_befehlsregister_q(7 downto 0) <= Befehlsregister_Operand_q(7 downto 0);

  ext_befehlsreg_data_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => register_data_in_ext,
      data1 => r_ext_befehlsreg_data_mux_in,
      result => r_ext_befehlsreg_data_mux_out,
      sel => cpu_en
    );
  ext_befehlsreg_load_mux : entity work.mux_2_n
    generic map(
      size => 1
    )
    port map(
      data0 => ext_reg_load_demux_out(1 downto 1),
      data1 => Steuerwerk_load_register(1 downto 1),
      result => load_register(1 downto 1),
      sel => cpu_en
    );

  Befehlsregister_OPCode : entity work.register_en
    generic map (
      size        => 8,
      clear_value => 0
      )
    port map (
      clock_t          => stim_n_0_clk_t,
      -- load             => steuerautomat_0_load_befehlsregister,
      load             => load_register(1),
      clr              => steuerautomat_0_clear_register,
      -- data => Speicher_UpperByte_n_Dout(7 downto 0),
      data => r_ext_befehlsreg_data_mux_out(15 downto 8),
      q(7 downto 0)    => Befehlsregister_OPCode_q(7 downto 0)
      );

  Befehlsregister_Operand_n : entity work.register_en
    generic map (
      size        => 8,
      clear_value => 0
      )
    port map (
      clock_t          => stim_n_0_clk_t,
      -- load             => steuerautomat_0_load_befehlsregister,
      load             => load_register(1),
      clr              => steuerautomat_0_clear_register,
      -- data => Speicher_LowerByte_Dout(7 downto 0),
      data => r_ext_befehlsreg_data_mux_out(7 downto 0),
      q(7 downto 0)    => Befehlsregister_Operand_q(7 downto 0)
      );
  Befehlsregister_Operand_q_16 <= "00000000" & Befehlsregister_Operand_q;


  -- --------------- Hilfsregister ---------------------------------------------
  ext_hilfsreg_data_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => register_data_in_ext,
      data1 => bus_tri_0_output(15 downto 0),
      result => r_ext_hilfsreg_data_mux_out,
      sel => cpu_en
    );
  ext_hilfsreg_load_mux : entity work.mux_2_n
    generic map(
      size => 1
    )
    port map(
      data0 => ext_reg_load_demux_out(2 downto 2),
      data1 => Steuerwerk_load_register(2 downto 2),
      result => load_register(2 downto 2),
      sel => cpu_en
    );

  Hilfsregister_n : entity work.register_en
    generic map (
      size        => 16,
      clear_value => 0
      )
    port map (
      clock_t           => stim_n_0_clk_t,
      -- load              => Steuerwerk_load_hilfsregister,
      load              => load_register(2),
      clr               => steuerautomat_0_clear_register,
      -- data => bus_tri_0_output(15 downto 0),
      data => r_ext_hilfsreg_data_mux_out,
      q(15 downto 0)    => Hilfsregister_q(15 downto 0)
      );

  -----------------------------------
  -- Programmzähler
  
  ext_pc_data_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => register_data_in_ext,
      data1 => programmzaehler_mux_out(15 downto 0),
      result => r_ext_pc_data_mux_out,
      sel => cpu_en
    );
  ext_pc_load_mux : entity work.mux_2_n
    generic map(
      size => 1
    )
    port map(
      data0 => ext_reg_load_demux_out(3 downto 3),
      data1 => Steuerwerk_load_register(3 downto 3),
      result => load_register(3 downto 3),
      sel => cpu_en
    );

  Programmzaehler : entity work.register_en
    generic map (
      size        => 16,
      clear_value => 0
      )
    port map (
      clock_t           => stim_n_0_clk_t,
      -- load              => Steuerwerk_load_programmzaehler,
      load              => load_register(3),
      clr               => steuerautomat_0_clear_register,
      -- data => programmzaehler_mux_out(15 downto 0),
      data => r_ext_pc_data_mux_out,
      q(15 downto 0)    => Programmzaehler_q(15 downto 0)
      );

  increment_n_0 : entity work.increment_n
    generic map (
      SIZE => 16
      )
    port map (
      inp  => Programmzaehler_q(15 downto 0),
      outp(15 downto 0) => Programmzaehler_inc(15 downto 0)  -- Programmzaehler_inc
      );

  -----------------------------------
  -- ALU
  alu_n_0 : entity work.alu_n
    port map (
      clock_t                    => stim_n_0_clk_t,
      cal                        => steuerautomat_0_calculate,
      equal_zero                 => alu_n_0_equal_zero,
      greater_zero               => ALU_greater_zero,
      less_zero                  => alu_n_0_less_zero,
      opcode(7 downto 0)         => Befehlsregister_OPCode_q(7 downto 0),
      operand_a_in(15 downto 0)  => Akku_q(15 downto 0),
      operand_b_in(15 downto 0)  => mux_2_n_0_result(15 downto 0),
      operand_c_out(15 downto 0) => ALU_operand_c_out(15 downto 0),
      reset_init                 => steuerautomat_0_clear_register
      );


  -----------------------------------
  -- Multiplexer

  Speicher_LowerByte_Dout(7 downto 0)        <= bus_tri_0_output(7 downto 0);
  Speicher_LowerByte_Dout_16                 <= "00000000" & Speicher_LowerByte_Dout;
  Speicher_UpperByte_n_Dout(7 downto 0)      <= bus_tri_0_output(15 downto 8);
  BefehlsregUpperByte_Zero_dout(15 downto 0) <= "00000000" & Befehlsregister_Operand_q(7 downto 0);

  mux_2_n_0 : entity work.mux_2_n
    generic map (
      size => 16
      )
    port map (
      data0  => bus_tri_0_output(15 downto 0),
      data1  => BefehlsregUpperByte_Zero_dout(15 downto 0),
      result(15 downto 0) => mux_2_n_0_result(15 downto 0),
      sel                 => Steuerwerk_alu_operand_b_select
      );
  mux_4_n_0 : entity work.mux_4_n
    generic map (
      size => 16
      )
    port map (
      data0  => BefehlsregUpperByte_Zero_dout(15 downto 0),
      data1  => bus_tri_0_output(15 downto 0),
      data2  => ALU_operand_c_out(15 downto 0),
      data3  => Hilfsregister_q(15 downto 0),
      result(15 downto 0) => mux_4_n_0_result(15 downto 0),
      sel(1 downto 0)     => Steuerwerk_akku_data_in_select(1 downto 0)
      );
  mux_4_n_1 : entity work.mux_4_n
    generic map (
      size => 16
      )
    port map (
      data0  => BefehlsregUpperByte_Zero_dout(15 downto 0),
      data1  => Akku_q(15 downto 0),
      data2  => ALU_operand_c_out(15 downto 0),
      data3  => Hilfsregister_q(15 downto 0),
      result(15 downto 0) => mux_4_n_1_result(15 downto 0),
      sel(1 downto 0)     => Steuerwerk_speicher_data_in_select(1 downto 0)
      );

  address_mux : entity work.mux_4_n
    generic map (
      size => 16
      )
    port map (
      data0  => Befehlsregister_Operand_q_16,  -- ist 8 Bit
      data1  => Programmzaehler_q(15 downto 0),
      data2  => Hilfsregister_q(15 downto 0),
      data3  => "0000000000000000",
      result(15 downto 0) => address_multiplexer_out(15 downto 0),  -- address_multiplexer_out
      sel(1 downto 0)     => Steuerwerk_address_select(1 downto 0)
      );

  programmzaehler_mux : entity work.mux_4_n
    generic map (
      size => 16
      )
    port map (
      data0  => Programmzaehler_inc(15 downto 0),
      data1  => Befehlsregister_Operand_q_16,
      data2  => bus_tri_0_output,  -- Speicher_LowerByte_Dout_16,
      data3  => "0000000000000000",
      result(15 downto 0) => programmzaehler_mux_out(15 downto 0),  -- programmzaehler_mux_out
      sel(1 downto 0)     => Steuerwerk_programmzaehler_data_in_select(1 downto 0)
      );


  --- Mux ram_address, ram_data_in, addrstrb and wnr from external when cpu is stopped. This allows to allow external alteration of the RAM.
  ext_ram_addr_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => ram_address_ext(15 downto 0),
      data1 => address_multiplexer_out(15 downto 0),
      result => r_ext_ram_mux_out(17 downto 2),
      sel => cpu_en
    );

  ext_ram_data_mux : entity work.mux_2_n
    generic map(
      size => 16
    )
    port map(
      data0 => ram_data_in_ext(15 downto 0),
      data1 => mux_4_n_1_result(15 downto 0),
      result(15 downto 0) => r_ext_ram_mux_out(33 downto 18),
      sel => cpu_en
    );

  ext_ram_ctrl_mux : entity work.mux_2_n
    generic map(
      size => 2
    )
    port map(
      data0 => data0_ext_ram_ctrl_mux,
      data1 => data1_ext_ram_ctrl_mux,
      result(0) => r_ext_ram_mux_out(0),
      result(1) => r_ext_ram_mux_out(1),
      sel => cpu_en
    );

data0_ext_ram_ctrl_mux(0) <= addrstrb_ext 		;
data0_ext_ram_ctrl_mux(1) <= wnr_ext 			;
data1_ext_ram_ctrl_mux(0) <= Steuerwerk_strobe 	;
data1_ext_ram_ctrl_mux(1) <= Steuerwerk_wr 		;


  svnr_mem_1 : entity work.svnr_mem
    port map (
      clk                      => clk_to_mem,
      addrstrb                 => r_ext_ram_mux_out(0),
      ram_address(15 downto 0) => r_ext_ram_mux_out(17 downto 2),  -- 
      ram_data_in              => r_ext_ram_mux_out(33 downto 18),
      ram_data_out             => bus_tri_0_output,
      reset_init               => stim_0_reset,
      beep                     => beep,
      ws2812_out               => ws2812_out,
      led                      => led,
      btn                      => btn,
      sw                       => sw,
      PMOD1                    => anzeige_2_hexdigits,
      wnr                      => r_ext_ram_mux_out(1),
      reset_mem                => reset_mem,
      mem_init_running         => mem_init_running
      );


  zehner <= anzeige_2_hexdigits(7 downto 4);
  einer  <= anzeige_2_hexdigits(3 downto 0);

  -- steuerautomat_reset_int <= stim_0_reset or reset_ext;
  steuerautomat_reset_int <= reset_ext;

  steuerautomat_n_0 : entity work.steuerautomat_n
    port map (
      reset_init                                 => steuerautomat_reset_int,
      clk_t                                      => stim_n_0_clk_t,
      opcode(7 downto 0)                         => Befehlsregister_OPCode_q(7 downto 0),
      greater_zero                               => ALU_greater_zero,
      equal_zero                                 => alu_n_0_equal_zero,
      less_zero                                  => alu_n_0_less_zero,
      clear_register                             => steuerautomat_0_clear_register,
      strobe                                     => Steuerwerk_strobe,
      wr                                         => Steuerwerk_wr,
      address_select(1 downto 0)                 => Steuerwerk_address_select(1 downto 0),
      speicher_data_in_select(1 downto 0)        => Steuerwerk_speicher_data_in_select(1 downto 0),
      akku_data_in_select(1 downto 0)            => Steuerwerk_akku_data_in_select(1 downto 0),
      load_akku                                  => Steuerwerk_load_akku,
      load_befehlsregister                       => steuerautomat_0_load_befehlsregister,
      load_programmzaehler                       => Steuerwerk_load_programmzaehler,
      programmzaehler_data_in_select(1 downto 0) => Steuerwerk_programmzaehler_data_in_select(1 downto 0),
      load_hilfsregister                         => Steuerwerk_load_hilfsregister,
      alu_operand_b_select                       => Steuerwerk_alu_operand_b_select,
      calculate                                  => steuerautomat_0_calculate,
      cpu_en                                     => cpu_en,
      o_step_fin                                 => cpu_step_fin
      );

  clkreset_1 : entity work.clkreset
    port map (
      clk_input_pin    => clk,
      mem_init_running => mem_init_running,
      clk_t            => r_clk_out,
      reset_init       => stim_0_reset,
      reset_mem        => reset_mem
    );

  clk_to_mem <= r_clk_out;
  stim_n_0_clk_t <= r_clk_out;
 
end STRUCTURE;
